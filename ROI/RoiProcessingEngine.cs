using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ROI.Model;
using ROI.ROIService;
using Order = ROI.Model.Order;

namespace ROI
{
    public class RoiProcessingEngine
    {
        static readonly string Token = EncryptHelper.Encrypt("letmein", "KingStor");
        public static readonly ROIWebInterfaceSoapClient RoiClient = new ROIWebInterfaceSoapClient();
        public static readonly Logger Logger = new Logger();

        public static async Task ProcessRoiOrders()
        {
            OrderManager orderManager = new OrderManager();

            var cutOffDate = new DateTime(2020, 1, 1);
            var days = DateTime.Now.Subtract(cutOffDate).Days;
            var orderIds = RoiClient.GetOrderIdListByDateRange(DateTime.Now.AddDays(-1 * days), DateTime.Now, Token);

            Dictionary<string, string> messages = new Dictionary<string, string>();

            foreach (var oid in orderIds)
            {               
                if (orderManager.OrderAlreadyPushedToTharstern(Convert.ToInt64(oid)))
                    continue;

                if (orderManager.OrderFailedInProcessing(Convert.ToInt64(oid)))
                    continue;

                Order order = new Order();

                //Order Details
                var orderDetails = RoiClient.GetOrder(oid, Token);

                bool cancelledOrder = false;

                cancelledOrder = IsCancelledOrder(messages, orderDetails, false);

                if (cancelledOrder)
                    continue;

                SetPoNumber(oid, order);

                var user = RoiClient.GetUserById(orderDetails.userId, Token);

                var accCode = user.CustomUserFields[25].Value; //UserProfileAccountNumber

                bool isOlam = IsOlamCustomer(user);

                if (isOlam)
                {

                    orderManager.AddNonReadyOrders(oid, "Rejected - OLAM");

                    if (!messages.ContainsKey(oid))
                        messages.Add(oid, "Olam account order - please process this order manually.");
                    else
                        messages[oid] += " | " + "Olam account order - please process this order manually.";

                    continue;
                }
                //check account number exists

                if (CheckCustomerCode(oid, accCode, messages))
                    continue;

                order.RoiOrderId = oid;
                order.User = user.LoginName;
                order.AccountCode = accCode;
                order.OrderTotal = orderDetails.orderTotal.amount;
                order.CurrencyUnit = orderDetails.orderTotal.unit.ToString();
                order.OrderPlacedDate = orderDetails.dateTimeOrderPlaced;
                order.User = orderDetails.userId;
                order.OrderPartsCount = orderDetails.documentIdList.Count;

                orderManager.DownloadRoiOrder(Convert.ToInt64(oid), orderDetails.orderTotal.amount, orderDetails.dateTimeOrderPlaced);

                List<PrintOptions> printOptionsList = new List<PrintOptions>();

                DeliveryDetails deliveryDetails = new DeliveryDetails();

                ProcessOrderItems(orderDetails, printOptionsList, deliveryDetails, messages);

                if (messages.ContainsKey(oid))
                    continue;

                if (orderManager.CheckNotReadyOrders(orderDetails.orderId))
                    continue;

                order.Email = deliveryDetails.Reference;
                order.UserName = deliveryDetails.UserName;
                order.ExternalId = orderDetails.externalId;

                bool continueFlag = true;

                foreach (var printoption in printOptionsList)
                {
                    if (string.IsNullOrEmpty(printoption.Size))
                    {
                        if (!messages.ContainsKey(oid))
                            messages.Add(oid, "Product option SIZE Missing, product option may be html literal, please review the product option");
                        else
                            messages[oid] += " | " + "Product option SIZE Missing, product option may be html literal, please review the product option";
                        continueFlag = false;

                    }

                    if (string.IsNullOrEmpty(printoption.Stock))
                    {
                        if (!messages.ContainsKey(oid))
                            messages.Add(oid, "Product option STOCK Missing, product option may be html literal, please review the product option");
                        else
                            messages[oid] += " | " + "Product option STOCK Missing, product option may be html literal, please review the product option";

                        continueFlag = false;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(printoption.StockFullString))
                        {
                            bool containsInt = printoption.StockFullString.Any(char.IsDigit);

                            if (!containsInt)
                            {
                                if (!messages.ContainsKey(oid))
                                    messages.Add(oid, "Invalid Stock value: " + printoption.StockFullString + ", product option may be html literal, please review the product option");
                                else
                                    messages[oid] += " | " + "Invalid Stock value: " + printoption.StockFullString + ", product option may be html literal, please review the product option";

                                continueFlag = false;
                            }
                        }
                    }
                }

                if (continueFlag)
                {
                    AddressDetails customerAddress = BuildCustomerAddress(user);

                    await TharsternSubmission(printOptionsList, deliveryDetails, order, messages, customerAddress);

                    orderManager.MarkOrderAllPartsPushedToTharstern(Convert.ToInt64(oid), printOptionsList.Count);

                }

            }

            //send email

            SendProcessingSummaryEmail(messages);
        }

        private static bool IsOlamCustomer(User user)
        {
            var olamAccount = false;

            foreach (var field in user.CustomUserFields)
            {
                if (field.Name == "UserProfileAccountNumber")
                {
                    if (field.Value == "OLA01_KF")
                        olamAccount = true;
                }

                if (field.Name == "UserProfileAccount")
                {
                    if (field.Value == "Olam")
                        olamAccount = true;
                }

            }

            return olamAccount;

        }

        public static void SendProcessingSummaryEmail(Dictionary<string, string> messages)
        {
            var defaultMessage = EmailHelper.AutoOrderPushNotificationResults;

            var orderstatuscontent = "";

            orderstatuscontent += "<table border='1'><tr><td colspan='1'><strong>Order ID</strong></td><td colspan='1'><strong>Status</strong></td></tr>";

            var orderStatusdetails = "";

            if (messages.Keys.Count == 0)
                return;

            foreach (var key in messages.Keys)
            {

                orderStatusdetails += "<tr>";
                orderStatusdetails += "<td>" + key + "</td>";

                orderStatusdetails += "<td>" + messages[key] + "</td>";

                orderStatusdetails += "</tr>";
            }

            orderstatuscontent += orderStatusdetails;
            orderstatuscontent += "</table>";

            defaultMessage = Regex.Replace(defaultMessage, "\\[ORDERSTATUS\\]", orderstatuscontent);

            var emails = ConfigurationManager.AppSettings["NotificationEmails"].Split(new char[] { ';' });

            foreach (var email in emails)
            {
                if (string.IsNullOrEmpty(email))
                    continue;

                var timeNow = DateTime.Now.ToString("MM/dd/yyyy H:mm:ss");

                EmailHelper.SendMail(email,
                          "ROI Order Summary - " + timeNow, defaultMessage);
            }
        }

        public static AddressDetails BuildCustomerAddress(User user)
        {
            AddressDetails customerAddress = new AddressDetails();

            foreach (var field in user.CustomUserFields)
            {
                switch (field.Name)
                {
                    case "UserProfileEmailAddress":
                        customerAddress.Email = field.Value;
                        break;

                    case "UserProfileFirstName":
                        customerAddress.FirstName = field.Value;
                        break;

                    case "UserProfileLastName":
                        customerAddress.LastName = field.Value;
                        break;

                    case "UserProfileAddress1":
                        customerAddress.Line1 = field.Value;
                        break;

                    case "UserProfileAddress2":
                        customerAddress.Line2 = field.Value;
                        break;

                    case "UserProfileCity":
                        customerAddress.City = field.Value;
                        break;
                    case "UserProfileCounty":
                        customerAddress.County = field.Value;
                        break;
                    case "UserProfilePostalCode":
                        customerAddress.PostCode = field.Value;
                        break;

                    case "UserProfileCountry":
                        customerAddress.Country = field.Value;
                        break;

                    case "UserProfilePhone1":
                        customerAddress.Telephone = field.Value;
                        break;

                    case "UserProfilePhone3":

                        if (string.IsNullOrEmpty(customerAddress.Telephone) && !string.IsNullOrEmpty(field.Value))
                        {
                            customerAddress.Telephone = field.Value;
                        }

                        break;

                    case "UserProfileMobile":
                        customerAddress.Mobile = field.Value;
                        break;


                }

            }
            return customerAddress;
        }

        public static bool IsCancelledOrder(Dictionary<string, string> messages, ROIService.Order orderDetails, bool cancelledOrder)
        {
            foreach (var documentid in orderDetails.documentIdList)
            {
                var orderItem = RoiClient.GetOrderItem(documentid, Token);


                if (orderItem.Status.ToString() == "Unreviewed")
                {
                    cancelledOrder = true;

                    if (!messages.ContainsKey(orderDetails.orderId))
                        messages.Add(orderDetails.orderId, "Unreviewed Order  - will be processed later");
                    else
                        messages[orderDetails.orderId] += " | " + "Unreviewed Order  - will be processed later";
                    break;
                }


                if (orderItem.Status.ToString() == "Rejected")
                {
                    cancelledOrder = true;

                    OrderManager orderManager = new OrderManager();

                    if (!orderManager.CheckNotReadyOrders(orderDetails.orderId))
                    {
                        orderManager.AddNonReadyOrders(orderDetails.orderId, "Rejected");

                        if (!messages.ContainsKey(orderDetails.orderId))
                            messages.Add(orderDetails.orderId, "Cancelled Order");
                        else
                            messages[orderDetails.orderId] += " | " + "Cancelled Order";
                    }

                    break;
                }
            }



            return cancelledOrder;
        }

        public static void SetPoNumber(string oid, Order order)
        {
            var orderFields = RoiClient.GetOrderFields(oid, Token);

            foreach (var field in orderFields)
            {
                if (field.Name == "PaymentPurchaseOrderNumber")
                {
                    var poNumber = field.Value;
                    order.PoNumber = poNumber;
                    break;
                }
            }
        }

        public static void ProcessOrderItems(ROIService.Order orderDetails, List<PrintOptions> printOptionsList, DeliveryDetails deliveryDetails, Dictionary<string, string> messages)
        {
            //Order Items
            foreach (var documentid in orderDetails.documentIdList)
            {
                var orderItem = RoiClient.GetOrderItem(documentid, Token);


                if (orderItem.Status.ToString() == "Rejected")
                {
                    if (!messages.ContainsKey(orderDetails.orderId))
                        messages.Add(orderDetails.orderId, "Cancelled Order");
                    else
                        messages[orderDetails.orderId] += " | " + "Cancelled Order";
                    break;
                }
                //Options

                PrintOptions printOptions = new PrintOptions { Price = Convert.ToString(orderItem.Price.amount) };

                GetPrintOptions(orderItem, printOptions);

                printOptions.DocumentId = orderItem.ExternalId;


                string artworkPdf;
                try
                {
                    artworkPdf = "https://artwork.roi360.co.uk/kingfisher/" + RoiClient.GetOrderItemOutputPath(documentid, Token)[0];
                }
                catch (Exception ex)
                {
                    var orderManager = new OrderManager();

                    if (!orderManager.CheckNotReadyOrders(orderDetails.orderId))
                    {
                        orderManager.AddNonReadyOrders(orderDetails.orderId, "Artwork not found");

                        if (!messages.ContainsKey(orderDetails.orderId))
                            messages.Add(orderDetails.orderId, "Artwork not found ");
                        else
                            messages[orderDetails.orderId] += " | " + "Artwork not found ";
                    }


                    break;
                }
                printOptions.ArtworkUrl = artworkPdf;

                AddressDetails shippingAddress = new AddressDetails();

                GetshippingAddressFromOrderItem(orderItem, shippingAddress);

                var companyNameFromUser = shippingAddress.CompanyName;


                foreach (var field in orderDetails.CustomShippingFields)
                {
                    if (field.Name == "ShippingCompany")
                    {
                        shippingAddress.CompanyName = field.Value;
                        break;
                    }

                }

                if (string.IsNullOrEmpty(shippingAddress.CompanyName))
                    shippingAddress.CompanyName = companyNameFromUser;


                if (string.IsNullOrEmpty(shippingAddress.Email))
                {
                    //read from orderDetails
                    foreach (var field in orderDetails.CustomShippingFields)
                    {
                        switch (field.Name)
                        {
                            case "ShippingEmail":
                                shippingAddress.Email = field.Value;
                                break;
                        }

                    }
                }

                var product = RoiClient.GetProduct(orderItem.ProductId, Token);

                printOptions.ProductName = product.Name;
                printOptions.ProductType = product.Description;


                foreach (var field in product.CustomProductFields)
                {
                    switch (field.Name)
                    {
                        case "Finishing":
                            printOptions.Finishing = field.Value;
                            break;
                        case "Binding_Style":
                            printOptions.Binding = field.Value;
                            break;

                        case "PrintMethod":
                            printOptions.PrintMethod = field.Value;
                            break;
                        case "ShuttleworthEstimateID":
                            printOptions.EstimateId = field.Value;
                            break;
                        case "Turnaround":
                            printOptions.TurnAround = field.Value;
                            break;
                        case "FileCopies":

                            if (string.IsNullOrEmpty(field.Value))
                                printOptions.FileCopies = "0";
                            else
                            {
                                if (field.Value.ToLower() == "none")
                                    printOptions.FileCopies = "0";
                                else
                                {
                                    var filecopy = field.Value.Replace("file copies to go to Capture", "");
                                    filecopy = filecopy.Trim();
                                    printOptions.FileCopies = filecopy;
                                }
                            }
                            break;
                        case "Colours":
                            if (!string.IsNullOrEmpty(field.Value))
                            {
                                var colourPart = field.Value.Split(new char[] { '/' });

                                if (colourPart.Length == 2)
                                {
                                    printOptions.ColoursFront = colourPart[0];
                                    printOptions.ColoursBack = colourPart[1];
                                }
                            }
                            break;
                    }
                }

                printOptionsList.Add(printOptions);

                BuildDeliveryAddressForTharstern(deliveryDetails, shippingAddress);
            }
        }

        public static bool CheckCustomerCode(string oid, string accCode, Dictionary<string, string> messages)
        {
            if (string.IsNullOrEmpty(accCode))
            {
                var orderManager = new OrderManager();

                if (!orderManager.CheckNotReadyOrders(oid))
                {
                    orderManager.AddNonReadyOrders(oid, "Customer Account Code Missing");

                    if (!messages.ContainsKey(oid))
                        messages.Add(oid, "Customer Account Code Missing - " + oid + ", order rejected.");
                    else
                        messages[oid] += "Customer Account Code Missing - " + oid + ", order rejected.";
                }

            }

            return string.IsNullOrEmpty(accCode);
        }

        public static void BuildDeliveryAddressForTharstern(DeliveryDetails dd, AddressDetails shippingAddress)
        {
            dd.Line1 = shippingAddress.Line1;
            dd.Line2 = shippingAddress.Line2;
            dd.CompanyName = shippingAddress.CompanyName;
            dd.PostCode = shippingAddress.PostCode;
            dd.City = shippingAddress.City;
            dd.Country = shippingAddress.Country;
            dd.Description = shippingAddress.Description;
            dd.State = shippingAddress.State;
            dd.Reference = shippingAddress.Email;
            dd.UserName = shippingAddress.FirstName + " " + shippingAddress.LastName;
        }

        public static async Task<List<string>> TharsternSubmission(List<PrintOptions> printOptions, DeliveryDetails deliveryDetails, Order orderDetails, Dictionary<string, string> messages, AddressDetails customerAddress)
        {
            List<string> lstQuotesAndErrors = new List<string>();
            try
            {
                Logger.WriteLog("############ Tharstern - Order Submission started for PDQOrderId:" + orderDetails.RoiOrderId + " " + "############");

                TharsternEngine tharsternEngine = new TharsternEngine(printOptions, deliveryDetails);

                var po = orderDetails.PoNumber;

                lstQuotesAndErrors = await tharsternEngine.SubEstimateToTharstern(orderDetails, po, messages, customerAddress);
                Logger.WriteLog("############ Tharstern - Order Submission completed for PDQOrderId:" + orderDetails.RoiOrderId + " " + "############");

            }
            catch (Exception ex)
            {
                if (ex.InnerException != null) Logger.WriteLog(ex.InnerException.ToString());
                Logger.WriteLog("############ Tharstern - Order Submission failed for PDQOrderId:" + orderDetails.RoiOrderId + " " + ex.Message + "############");
            }
            return lstQuotesAndErrors;

        }

        public static void GetPrintOptions(OrderItem orderItem, PrintOptions printOptions)
        {
            try
            {
                printOptions.Description = orderItem.Description;
                printOptions.Status = orderItem.Status.ToString();

                for (int i = 0; i < orderItem.CustomPrintingFields.Length; i++)
                {

                    var field = orderItem.CustomPrintingFields[i];

                    if (field.Name == "PageCount" || field.Name == "PrintingPageCount")
                    {
                        try
                        {
                            List<string> split = field.Value.Split(' ').ToList();
                            printOptions.PageCount = split[0].Replace("pp", "");

                            if (split[1].Contains('/')) //PROBS NEED TO REMOVE AFTER
                            {
                                List<string> colours = split[1].Split('/').ToList();
                                printOptions.ColoursFront = colours[0];
                                printOptions.ColoursBack = colours[1];
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                    if (field.Name == "Colours")
                    {
                        if (field.Value.Contains('/'))
                        {
                            List<string> colours = field.Value.Split('/').ToList();
                            printOptions.ColoursFront = colours[0];
                            printOptions.ColoursBack = colours[1];
                        }
                    }
                    if (field.Name == "BindingStyleText")
                        printOptions.Binding = field.Value;

                    if (field.Name == "Binding_Style")
                        printOptions.Binding = field.Value;

                    if (field.Name == "Orientation")
                    {
                        //USING ORIENTATION AS BINDING
                        printOptions.Binding = field.Value;
                    }

                    if (field.Name == "Instructions")
                        printOptions.Instructions = field.Value;

                    if (field.Name == "PrintingQuantity")
                        printOptions.Quantity = field.Value;

                    if (field.Name == "PrintingTrimSize")
                        printOptions.TrimSize = field.Value;

                    if (field.Name == "Size")
                    {
                        if (string.IsNullOrEmpty(field.Value))
                            continue;

                        try
                        {
                            List<string> split = field.Value.Split('x').ToList();
                            printOptions.Width = split[0];
                            printOptions.Height = split[1];
                            printOptions.Size = field.Value;
                            printOptions.Orientation = Convert.ToInt32(printOptions.Width) >= Convert.ToInt32(printOptions.Height) ? "landscape" : "portrait";

                        }
                        catch (Exception ex)
                        {

                        }

                    }
                    if (field.Name == "Lamination")
                        printOptions.Lamination = field.Value;

                    if (field.Name == "Stock")
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(field.Value))
                                continue;

                            //SILK 400GSM, SILK 250GSM 

                            //if (field.Value == "Knight Frank Uncoated")
                            //{
                            //    field.Value = "350GSM VISION SUPERIOR, 250GSM VISION SUPERIOR";

                            //}

                            printOptions.StockFullString = field.Value;

                            if (field.Value.ToString().Any(char.IsDigit))
                            {
                                List<string> splitParts = field.Value.Split(',').ToList();

                                if (splitParts.Count() == 1)
                                {
                                    List<string> gsmStockSplit = splitParts[0].Split(' ').ToList();
                                    printOptions.Gsm = gsmStockSplit[0].ToLower().Replace("gsm", "");

                                    for (int j = 1; j < gsmStockSplit.Count; j++)
                                    {
                                        printOptions.Stock += gsmStockSplit[j] + " ";
                                    }

                                    printOptions.Stock = printOptions.Stock.Trim();

                                }

                                if (splitParts.Count() == 2) // 2 part product
                                {
                                    List<string> gsmStockSplit = splitParts[0].Split(' ').ToList();

                                    gsmStockSplit[0] = gsmStockSplit[0].ToLower();

                                    printOptions.CoverGsm = gsmStockSplit[0].Replace("gsm", "");


                                    for (int j = 1; j < gsmStockSplit.Count; j++)
                                    {
                                        printOptions.CoverStock += gsmStockSplit[j] + " ";
                                    }


                                    splitParts[1] = splitParts[1].Trim();

                                    gsmStockSplit = splitParts[1].Split(' ').ToList();

                                    gsmStockSplit[0] = gsmStockSplit[0].ToLower();

                                    printOptions.Gsm = gsmStockSplit[0].Replace("gsm", "");


                                    for (int j = 1; j < gsmStockSplit.Count; j++)
                                    {
                                        printOptions.Stock += gsmStockSplit[j] + " ";
                                    }

                                }
                            }
                            else
                            {

                                printOptions.Stock = field.Value;
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        public static void GetshippingAddressFromOrderItem(OrderItem orderItem, AddressDetails shippingAddress)
        {
            foreach (var field in orderItem.CustomFormFillFields)
            {
                if (field.Name == "Del_Company_Name")
                    shippingAddress.CompanyName = field.Value;

                if (field.Name == "Del_First_Name")
                    shippingAddress.FirstName = field.Value;

                if (field.Name == "Del_Last_Name")
                    shippingAddress.LastName = field.Value;

                if (field.Name == "Del_Address_1")
                    shippingAddress.Line1 = field.Value;

                if (field.Name == "Del_Address_2")
                    shippingAddress.Line2 = field.Value;

                if (field.Name == "Del_City")
                    shippingAddress.City = field.Value;

                if (field.Name == "Del_County")
                    shippingAddress.County = field.Value;

                if (field.Name == "Del_Postcode")
                    shippingAddress.PostCode = field.Value;

                if (field.Name == "TelNo")
                    shippingAddress.Telephone = field.Value;

                if (field.Name == "MobNo")
                    shippingAddress.Mobile = field.Value;

                if (field.Name == "Email")
                    shippingAddress.Email = field.Value;
            }
        }

        public static void GetShippingAddressFromOrder(ROIService.Order orderDetails, AddressDetails shippingAddress)
        {
            foreach (var field in orderDetails.CustomShippingFields)
            {
                if (field.Name == "ShippingCompanyName")
                    shippingAddress.CompanyName = field.Value;

                if (field.Name == "ShippingFirstName")
                    shippingAddress.FirstName = field.Value;

                if (field.Name == "ShippingLastName")
                    shippingAddress.LastName = field.Value;

                if (field.Name == "ShippingAddress1")
                    shippingAddress.Line1 = field.Value;

                if (field.Name == "ShippingAddress2")
                    shippingAddress.Line2 = field.Value;

                if (field.Name == "ShippingAddress3")
                    shippingAddress.Line3 = field.Value;

                if (field.Name == "ShippingAddress4")
                    shippingAddress.Line4 = field.Value;

                if (field.Name == "ShippingCity")
                    shippingAddress.City = field.Value;

                if (field.Name == "ShippingCounty")
                    shippingAddress.County = field.Value;

                if (field.Name == "ShippingState")
                    shippingAddress.State = field.Value;

                if (field.Name == "ShippingPostalCode")
                    shippingAddress.PostCode = field.Value;

                if (field.Name == "ShippingCountry")
                    shippingAddress.Country = field.Value;

                if (field.Name == "ShippingDescription")
                    shippingAddress.Description = field.Value;

                if (field.Name == "ShippingTelephone")
                    shippingAddress.Telephone = field.Value;

                if (field.Name == "ShippingEmail")
                    shippingAddress.Email = field.Value;
            }
        }

        public static async Task GetOrderedProductNames()
        {
            OrderManager orderManager = new OrderManager();

            //Get Order Ids
            var orderIds = RoiClient.GetOrderIdListByDateRange(System.DateTime.Now.AddDays(-350), System.DateTime.Now, Token);


            var productNames = new List<string>();
            foreach (var oid in orderIds)
            {

                try
                {

                    //Order Details
                    var orderDetails = RoiClient.GetOrder(oid, Token);

                    //Order Items
                    foreach (var documentid in orderDetails.documentIdList)
                    {
                        var orderItem = RoiClient.GetOrderItem(documentid.ToString(), Token);

                        //Options


                        var product2 = RoiClient.GetProduct(orderItem.ProductId, Token);

                        if (!productNames.Contains(product2.Name))
                        {
                            productNames.Add(product2.Name);
                        }


                        continue;


                    }
                }
                catch { }


            }

            var productsAll = string.Join(",", productNames);

        }

        public static async Task GetActiveProducts()
        {
            var activeProds = RoiClient.GetProductIdListOfActiveProducts(Token);


            List<string> productNames = new List<string>();
            foreach (var id in activeProds)
            {

                try
                {

                    var product2 = RoiClient.GetProduct(id, Token);


                    if (!productNames.Contains(product2.Name))
                    {
                        productNames.Add(product2.Name);
                    }
                }
                catch { }


            }

            var productsAll = string.Join(",", productNames);

        }

        public static void GetUsersThatHavePlacedOrders()
        {
            //Get Order Ids
            var orderIds = RoiClient.GetOrderIdListByDateRange(System.DateTime.Now.AddDays(-10), System.DateTime.Now, Token);


            //GET USERS THAT HAVE PLACED ORDERS
            List<string> users = new List<string>();

            foreach (var oid in orderIds)
            {
                //Order Details
                var orderDetails = RoiClient.GetOrder(oid, Token);
                var user = RoiClient.GetUserById(orderDetails.userId, Token);

                var accCode = user.CustomUserFields.Where(x => x.Name == "UserProfileAccountNumber");

                var accCode2 = user.CustomUserFields[25].Value;


                if (!users.Contains(user.LoginName))
                    users.Add(user.LoginName);
            }
        }
    }
}
