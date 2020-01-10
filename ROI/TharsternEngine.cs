using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using nsTharsternAPI;
using ROI.Entity;
using ROI.Model;
using ROI.Interface;
using System.Net;

namespace ROI
{

    /// <summary>
    /// THARSTERN ENGINE - CREATE ROI OBJECT TO THARSTERN OBJECTS AND CREATE ESTIMATE/JOBS/DELIVERY NOTES IN THARSTERN
    /// </summary>
    public class TharsternEngine : ITharstern
    {
        private readonly ApiEngine _engine;
        private ROIEntities _ctx;
        private readonly List<PrintOptions> _printOptions;
        private readonly DeliveryDetails _dd;
        private static readonly Logger Logger = new Logger();

        public TharsternEngine(List<PrintOptions> printOptionList, DeliveryDetails dd)
        {
            _engine = new ApiEngine();
            _ctx = new ROIEntities();
            _printOptions = printOptionList;
            _dd = dd;
        }

        public async Task Authenticate()
        {
            _engine.ApiBaseUrl = "http://primoserver/tharsternapi";
            await _engine.SetApiTokenAsync("api@espcolour.co.uk", "th4rAP!");
        }

        public async Task CreateJobFromEstimate(string orderId, string documentId, string digiOrLitho)
        {
            if (_engine.EstimateId > 0)
            {
                var Ref2 = "";

                if (digiOrLitho.ToLower() == "digital")

                    Ref2 = "Waiting Approval";//Approved no proof required (D)
                else
                    Ref2 = "Waiting Approval"; //Approved

                TharJobFromEstimate jobFromEstimate =
                    new TharJobFromEstimate
                    {
                        EstimateID = _engine.EstimateId.ToString(),
                        Ref2 = Ref2,
                        Ref1 = orderId,
                        SetAsProductionReady = true,
                        SubmitToWorkflow = true,

                    };


                var serializedResultCreateJobJson = JsonConvert.SerializeObject(
                    jobFromEstimate,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                _engine.CreateJobFromEstimateRequest = serializedResultCreateJobJson;

                await _engine.CreateJobFromEstimateAsync();

                if (!string.IsNullOrEmpty(_engine.JobNo))
                {

                    var oid = Convert.ToInt64(orderId);
                    var order = _ctx.tOrders.FirstOrDefault(o => o.ROIOrderId == oid);

                    if (order != null)
                    {
                        var orderTharsternPushDetails = _ctx.tOrderTharsternPushDetails.FirstOrDefault(o => o.OrderID == order.OrderID && o.DocumentId == documentId);

                        if (orderTharsternPushDetails != null)
                        {
                            orderTharsternPushDetails.TharsternJobNo = _engine.JobNo;
                            _ctx.SaveChanges();
                        }
                    }
                }
            }

        }

        public async Task<string> GetEstRef(string orderId)
        {
            string keyword = "ROI Order Id: " + orderId;
            string estimateRef = string.Empty;
            await Authenticate();

            dynamic d = await _engine.GetObjectAsync("estimates", new NameValueCollection {
                    { "keyword", keyword }
                }
            );
            if (d != null)
            {
                if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0 && d.Details.Items != null && d.Details.Items.Count > 0)
                {

                    estimateRef = d.Details.Items[0].EstimateRef.ToString();
                }

                d = null;
            }

            return estimateRef;
        }

        public void SetPdfProofProcess(List<TharEstimateRequest.tharProcesses> listProcess, string digiOrLitho)
        {
            //PDF PROOF PROCESS
            TharEstimateRequest.tharProcesses pdfProcesses = new TharEstimateRequest.tharProcesses();
            pdfProcesses.ProcessTypeID = 7;
            pdfProcesses.Value = 1;
            //no pdf proof
            pdfProcesses.CostCentreID = 1311;
            listProcess.Add(pdfProcesses);

        }

        public async Task<List<string>> SubEstimateToTharstern(Order o, string po, Dictionary<string, string> messages, AddressDetails customerAddress)
        {
            await Authenticate();
            List<string> lstQuotesAndErrors = new List<string>();

            var artwork = "";
            foreach (var x in _printOptions)
            {
                if (CreateTharsternPushEntry(o, x)) continue;

                TharEstimateRequest tharEst = new TharEstimateRequest();
                var estFinishedSize = new TharEstimateRequest.tharEstFinishedSize();
                List<TharEstimateRequest.TharEstParts> listParts = new List<TharEstimateRequest.TharEstParts>();
                List<TharEstimateRequest.tharProcesses> listProcess = new List<TharEstimateRequest.tharProcesses>();
                TharEstimateRequest.tharEstBindingSide bindingSide = new TharEstimateRequest.tharEstBindingSide();
                artwork = x.ArtworkUrl;

                SetSize(estFinishedSize, x, tharEst); // Set size first so we can check A5 8pp in GetDigiorLithoFromPrintMethod

                string digiOrLitho = "";

                //for cheapest method, determine the digital or litho based on ESP rules
                if (x.PrintMethod == "Cheapest Method")
                {
                    digiOrLitho = GetDigiorLithoFromPrintMethod(o.RoiOrderId, x.Width, x.Height,
                        Convert.ToInt32(x.PageCount), estFinishedSize, x);
                }
                else
                {
                    digiOrLitho = x.PrintMethod.ToLower();
                }

                if (string.IsNullOrEmpty(digiOrLitho))
                {
                    lstQuotesAndErrors.Add("ERROR - Digi or Litho not found - " + x.DocumentId);

                    if (!messages.ContainsKey(o.RoiOrderId))
                        messages.Add(o.RoiOrderId, "Digi or Litho not found - " + x.DocumentId);
                    else
                        messages[o.RoiOrderId] += " | " + "Digi or Litho not found - " + x.DocumentId;

                    continue;
                }

                var fileCopyJobBag = "";

                if (digiOrLitho.ToLower() == "digital")
                    fileCopyJobBag = "1";
                else
                    fileCopyJobBag = "5";

                tharEst.Ref4 = fileCopyJobBag;
                tharEst.Ref5 = x.FileCopies;

                tharEst.Ref6 = "ROI";
                tharEst.Ref7 = o.ExternalId + " - " + x.DocumentId;

                tharEst.Title = "ROI ID: " + o.RoiOrderId;

                if (!string.IsNullOrEmpty(x.Binding))
                {
                    if (x.Binding.ToLower().Contains("left"))
                        tharEst.Title += " - Bind LE";

                    if (x.Binding.ToLower().Contains("top"))
                        tharEst.Title += " - Bind TE";
                }

                tharEst.Title += " - document id: " + x.DocumentId + " " + x.Description;


                SetPdfProofProcess(listProcess, digiOrLitho);

                SetOrientation(x, tharEst);

                tharEst.ProductTypeId = SetProductTypeId(Convert.ToInt32(x.PageCount), o.RoiOrderId.ToString(), digiOrLitho, x.Width, x.Height, x);

                if (tharEst.ProductTypeId == 0)
                {
                    lstQuotesAndErrors.Add("ERROR - Product type ID not found - " + x.DocumentId);

                    if (!messages.ContainsKey(o.RoiOrderId))
                        messages.Add(o.RoiOrderId, "Product type ID not found  - " + x.DocumentId);
                    else
                        messages[o.RoiOrderId] += " | " + "Product type ID not found  - " + x.DocumentId;

                    continue;
                }

                try
                {
                    GetQuantity(x, tharEst);
                }
                catch (Exception e)
                {
                    lstQuotesAndErrors.Add("ERROR - Invalid Quantity - " + x.DocumentId);

                    if (!messages.ContainsKey(o.RoiOrderId))
                        messages.Add(o.RoiOrderId, "Invalid Quantity  - " + "-" + x.DocumentId);
                    else
                        messages[o.RoiOrderId] += " | " + "Invalid Quantity  - " + "-" + x.DocumentId;

                    continue;
                }


                tharEst.RequiredDateTime = this.SetDate(digiOrLitho, o.OrderPlacedDate);

                tharEst.JobType = this.SetJobType(digiOrLitho);

                listProcess.Add(this.SetPackingProcess(x));

                SetDrillProcess(listProcess, x.Binding);

                //TODO SetFolderMakeUpProcess(listProcess);

                tharEst.Processes = listProcess;

                tharEst.Customer = this.SetCustomer(o);

                await SetBillingAndDeliveryAddress(o, tharEst, x.DocumentId, customerAddress);

                List<TharEstimateRequest.tharProcesses> listPartProcess = new List<TharEstimateRequest.tharProcesses>();
                //TODO NEED TO HANDLE TWO PART BROCHURES
                if (x.ProductType == "SS With Cover" || x.ProductType == "Perfect Bound")
                {
                    //TODO 2 PARTS
                    TharEstimateRequest.TharEstParts part1 = new TharEstimateRequest.TharEstParts
                    {
                        ProductTypePartId = GetProductTypePartId("cover", digiOrLitho, o.RoiOrderId, tharEst.ProductTypeId, x.DocumentId),
                        Name = "Cover",
                        Pages = 4,
                        PaperCode = GetPaperCode(x, o.RoiOrderId, digiOrLitho, "Cover", x.DocumentId),
                        FinishedSize = estFinishedSize,
                        GSM = string.IsNullOrEmpty(x.CoverGsm) != true ? Convert.ToInt32(x.CoverGsm) : Convert.ToInt32(x.Gsm)
                    };

                    try
                    {

                        SetBindingSide(x, tharEst.ProductTypeId, part1);

                        SetColoursAndOtherCoverage(x, part1);

                        SetCoatings(digiOrLitho, x, part1);

                        part1.SpecificPress = GetSpecificPress(x, digiOrLitho, part1);

                        SetDieCutProcess(x, listPartProcess);

                        SetFoldingScheme(x, part1);

                        if (tharEst.ProductTypeId == 36 || tharEst.ProductTypeId == 27) //FOLDED LEAFLETS CREASING PROCESS
                        {
                            SetCreasingProcessLeaflets(x, listPartProcess, digiOrLitho);

                            SetFoldingProcess(x, listPartProcess);
                        }

                        SetLamination(x, listPartProcess, digiOrLitho);

                        part1.Processes = listPartProcess;
                    }
                    catch
                    {

                    }
                    //if (!string.IsNullOrEmpty(x.spine))
                    //    part.SpineThickness = Convert.ToInt32(x.spine);

                    listParts.Add(part1);

                    TharEstimateRequest.TharEstParts part2 = new TharEstimateRequest.TharEstParts
                    {
                        ProductTypePartId = GetProductTypePartId("text", digiOrLitho, o.RoiOrderId, tharEst.ProductTypeId, x.DocumentId),
                        Name = "text",
                        Pages = Convert.ToInt32(x.PageCount) - 4,
                        PaperCode = GetPaperCode(x, o.RoiOrderId, digiOrLitho, "Text", x.DocumentId),
                        FinishedSize = estFinishedSize,
                        GSM = Convert.ToInt32(x.Gsm)
                    };

                    try
                    {
                        SetBindingSide(x, tharEst.ProductTypeId, part2);
                        SetColoursAndOtherCoverage(x, part2);

                        SetCoatings(digiOrLitho, x, part2);

                        part1.SpecificPress = GetSpecificPress(x, digiOrLitho, part2);

                        SetDieCutProcess(x, listPartProcess);

                        SetFoldingProcess(x, listPartProcess);

                        SetFoldingScheme(x, part2);

                        if (tharEst.ProductTypeId == 36 || tharEst.ProductTypeId == 27) //FOLDED LEAFLETS CREASING PROCESS
                        {
                            SetCreasingProcessLeaflets(x, listPartProcess, digiOrLitho);

                            SetFoldingProcess(x, listPartProcess);
                        }

                        SetLamination(x, listPartProcess, digiOrLitho);

                        part2.Processes = listPartProcess;
                    }
                    catch
                    {

                    }
                    //if (!string.IsNullOrEmpty(x.spine))
                    //    part.SpineThickness = Convert.ToInt32(x.spine);

                    listParts.Add(part2);
                }
                else
                { // ONE PART
                    TharEstimateRequest.TharEstParts part = new TharEstimateRequest.TharEstParts
                    {
                        ProductTypePartId = GetProductTypePartId(x.ProductName.ToLower(), digiOrLitho, o.RoiOrderId, tharEst.ProductTypeId, x.DocumentId),
                        Name = x.ProductName,
                        Pages = Convert.ToInt32(x.PageCount),
                        PaperCode = GetPaperCode(x, o.RoiOrderId, digiOrLitho, "", x.DocumentId),
                        FinishedSize = estFinishedSize,
                        GSM = Convert.ToInt32(x.Gsm)
                    };

                    try
                    {

                        SetBindingSide(x, tharEst.ProductTypeId, part);

                        SetColoursAndOtherCoverage(x, part);

                        SetCoatings(digiOrLitho, x, part);

                        part.SpecificPress = GetSpecificPress(x, digiOrLitho, part);

                        SetDieCutProcess(x, listPartProcess);

                        SetFoldingProcess(x, listPartProcess);

                        SetFoldingScheme(x, part);

                        if (tharEst.ProductTypeId == 36 || tharEst.ProductTypeId == 27) //FOLDED LEAFLETS CREASING PROCESS
                        {
                            SetCreasingProcessLeaflets(x, listPartProcess, digiOrLitho);

                            SetFoldingProcess(x, listPartProcess);
                        }

                        SetLamination(x, listPartProcess, digiOrLitho);

                        part.Processes = listPartProcess;
                    }
                    catch
                    {

                    }
                    //if (!string.IsNullOrEmpty(x.spine))
                    //    part.SpineThickness = Convert.ToInt32(x.spine);

                    listParts.Add(part);
                }

                bool valid = IsValidPaperCode(listParts);
                bool suppliedByClient = x.Stock.Contains("Supplied by Client");
                if (!valid && !suppliedByClient)
                {
                    lstQuotesAndErrors.Add("ERROR - Paper not valid - " + x.Stock + ", ROI document Id: " + x.DocumentId);

                    if (!messages.ContainsKey(o.RoiOrderId))
                        messages.Add(o.RoiOrderId, "Paper not valid  - " + x.Stock + ", ROI document Id: " + x.DocumentId);
                    else
                        messages[o.RoiOrderId] += " | " + "Paper not valid  - " + x.Stock + ", ROI document Id: " + x.DocumentId;


                    continue;
                }

                valid = IsValidProductType(listParts);

                if (!valid)
                {
                    lstQuotesAndErrors.Add("ERROR - Product type ID is not valid - " + x.DocumentId);

                    if (!messages.ContainsKey(o.RoiOrderId))
                        messages.Add(o.RoiOrderId, "Product type ID is not valid  - " + x.DocumentId);
                    else
                        messages[o.RoiOrderId] += " | " + "Product type ID is not valid  - " + x.DocumentId;

                    continue;
                }

                tharEst.Parts = listParts;

                decimal cost = decimal.Parse(x.Price);

                tharEst.TotalPrice = Math.Round(cost);

                var serializedResultJson = JsonConvert.SerializeObject(
                    tharEst,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });


                if (serializedResultJson.Contains(",{}"))
                    serializedResultJson = serializedResultJson.Replace(",{}", "");


                o.TharsternJson = serializedResultJson;

                DumpTharsternJson(o.RoiOrderId, serializedResultJson, x.DocumentId);

                _engine.EstRequestSample = serializedResultJson;

                await _engine.CreateEstimateAsync();

                if (string.IsNullOrEmpty(_engine.EstimateNo)) // BECAUSE THARSTERNS RETURNS SUCESS FALSE WHEN EST IS CREATED WE CHECK AGAIN
                    _engine.EstimateNo = await GetEstRef(o.RoiOrderId);

                if (!string.IsNullOrEmpty(_engine.EstimateNo))
                {
                    try
                    {
                        if (!RemoteFileExists(artwork))
                        {
                            if (!messages.ContainsKey(o.RoiOrderId))
                                messages.Add(o.RoiOrderId, "Artwork not found - " + artwork);
                            else
                                messages[o.RoiOrderId] += " | " + "Artwork not found - " + artwork;
                        }
                    }
                    catch { }

                    await SubmitPOandArtwork(o, po, artwork, x.ProductName, x.DocumentId);

                    //await SubmitArtowork(o, artwork, x.DocumentId);
                    //await SubmitPO(o, po, x.DocumentId);

                    await SubmitNotes(o, ""); // ANY NOTES IN ROI??
                    lstQuotesAndErrors.Add(_engine.EstimateNo);


                    var contactFound = false;

                    try
                    {
                        contactFound = Convert.ToBoolean(SearchContactInTharstern(o.AccountCode, customerAddress.FirstName + " " + customerAddress.LastName, customerAddress.Email));

                    }
                    catch { }

                    if (!Convert.ToBoolean(contactFound))
                        await CreateContact(o, customerAddress);


                    await CreateJobFromEstimate(o.RoiOrderId, x.DocumentId, digiOrLitho);

                    //create delivery note

                    var serializedResultJsonPreDelivery = CreatePreDeliveryNote(o, tharEst);

                    _engine.PreDeliveryJson = serializedResultJsonPreDelivery;

                    await _engine.CreatePreDeliveryAsync();

                    TharDataEntities _ctxThar = new TharDataEntities();

                    if (!string.IsNullOrEmpty(_engine.JobDeliveryNoteNo))
                    {
                        UpdateJobDelNote(x.DocumentId, _engine.JobDeliveryNoteNo);
                        UpdateDeliveryTypeId(_engine.JobDeliveryNoteNo, _ctxThar, 2);
                    }

                    //create delivery note customer

                    if (x.FileCopies != "0")
                    {
                        var serializedResultJsonPreDeliveryCustomer = CreatePreDeliveryNoteCustomer(customerAddress, o, tharEst, x.FileCopies);

                        _engine.PreDeliveryJsonCustomer = serializedResultJsonPreDeliveryCustomer;

                        await _engine.CreatePreDeliveryCustomerAsync();

                        if (!string.IsNullOrEmpty(_engine.CustomerDeliveryNoteNo))
                        {
                            UpdateCustomerDelNote(x.DocumentId, _engine.CustomerDeliveryNoteNo);
                            UpdateDeliveryTypeId(_engine.CustomerDeliveryNoteNo, _ctxThar, 4);
                        }

                    }

                }
                else
                {
                    Logger.WriteLog("ERROR - Tharstern estimate creation- failed for  ROI Order Id:" + o.RoiOrderId + ". Problems: " + _engine.EstimateError + " - Document- " + x.DocumentId);
                    UpdateLastMessage(Convert.ToInt64(o.RoiOrderId), "Thar est creation- FAILED - Problems: " + _engine.EstimateError, x.DocumentId);

                    if (!messages.ContainsKey(o.RoiOrderId))
                        messages.Add(o.RoiOrderId, "Tharstern estimate creation failed - " + _engine.EstimateError);
                    else
                        messages[o.RoiOrderId] += " | " + "Tharstern estimate creation failed - " + _engine.EstimateError;

                    MarkTharsternPushFailure(o.RoiOrderId);

                    continue;
                }

                if (!lstQuotesAndErrors.Contains("ERROR"))
                {
                    MarkTharsternPushSuccess(o.RoiOrderId, _engine.EstimateNo, o.TharsternJson, x.DocumentId);

                    if (!messages.ContainsKey(o.RoiOrderId))
                        messages.Add(o.RoiOrderId, "SUCCESS, Estimate NO- " + _engine.EstimateNo);
                    else
                        messages[o.RoiOrderId] += " | " + "SUCCESS, Estimate NO- " + _engine.EstimateNo;

                    if (!string.IsNullOrEmpty(_engine.JobNo))
                        messages[o.RoiOrderId] += " | " + "Job NO- " + _engine.JobNo;

                }

            }


            return lstQuotesAndErrors;
        }

        public async Task<bool> SearchContactInTharstern(string Customercode, string name, string email)
        {
            return await _engine.SearchContactForAccount(Customercode, name, email);

        }



        private async Task CreateContact(Order o, AddressDetails customerAddress)
        {

            //Create contact
            //account code = o.accountcode
            //rest details are in customerAddress

            try
            {
                ROI.Model.TharEstimateRequest.Contact contact = new TharEstimateRequest.Contact();

                contact.CustomerCode = o.AccountCode;
                contact.Name = customerAddress.FirstName + " " + customerAddress.LastName;
                contact.Mobile = customerAddress.Mobile;
                contact.Telephone = customerAddress.Telephone;
                contact.Email = customerAddress.Email;

                var serializedResultCreateContactJson = JsonConvert.SerializeObject(
                                  contact,
                                  new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                serializedResultCreateContactJson = "{ Items:[" + serializedResultCreateContactJson;

                serializedResultCreateContactJson = serializedResultCreateContactJson + "]}";
                _engine.CreateContactFromAccountRequest = serializedResultCreateContactJson;

                await _engine.CreateContactForAccount();
            }
            catch { }
        }

        private bool RemoteFileExists(string url)
        {
            try
            {
                //Creating the HttpWebRequest
                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                //Setting the Request method HEAD, you can also use GET too.
                request.Method = "HEAD";
                //Getting the Web Response.
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                //Returns TRUE if the Status code == 200
                response.Close();
                return (response.StatusCode == HttpStatusCode.OK);
            }
            catch
            {
                //Any exception will returns false.
                return false;
            }
        }

        private static void UpdateDeliveryTypeId(string customerDelNoteNo, TharDataEntities _ctxThar, byte deliveryTypeId)
        {
            try
            {
                int delNoteNo = Convert.ToInt32(customerDelNoteNo);

                if (delNoteNo > 0)
                {
                    var delNote = _ctxThar.DeliveryNote.FirstOrDefault(d => d.ID == delNoteNo);
                    delNote.DeliveryType = deliveryTypeId;
                    _ctxThar.SaveChanges();
                }
            }
            catch (Exception e)
            {
            }
        }

        public string CreatePreDeliveryNoteCustomer(AddressDetails customerAddress, Order o, TharEstimateRequest tharEst, string fileCopies)
        {
            PreDelivery preDelivery = new PreDelivery();
            preDelivery.RequiredDate = Convert.ToDateTime(tharEst.RequiredDateTime);
            preDelivery.JobNo = _engine.JobNo;
            preDelivery.AddressID = _engine.CustomerAddressId;
            preDelivery.Contact = customerAddress.FirstName + " " + customerAddress.LastName;
            preDelivery.ContactEmail = customerAddress.Email;
            var address = new Address();
            address.AddressLines = new List<string>();
            address.AddressLines.Add(customerAddress.CompanyName);
            address.AddressLines.Add(customerAddress.Line1);
            address.AddressLines.Add(customerAddress.Line2);
            address.City = customerAddress.City;
            address.Country = customerAddress.Country;
            address.Postcode = customerAddress.PostCode;
            address.Region = customerAddress.Line3 + " " + customerAddress.Line4;

            preDelivery.Address = address;
            preDelivery.Collection = false;
            var qty = 0;

            try
            {

                qty = Convert.ToInt32(fileCopies);
            }
            catch { }

            preDelivery.Quantity = qty;
            preDelivery.Ref1 = o.RoiOrderId;
            preDelivery.SubJobs = null;

            var serializedResultJsonPreDelivery = JsonConvert.SerializeObject(
                                                    preDelivery,
                                                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            return serializedResultJsonPreDelivery;
        }

        public string CreatePreDeliveryNote(Order o, TharEstimateRequest tharEst)
        {
            PreDelivery preDelivery = new PreDelivery();
            preDelivery.RequiredDate = Convert.ToDateTime(tharEst.RequiredDateTime);
            preDelivery.JobNo = _engine.JobNo;
            preDelivery.AddressID = _engine.DeliveryAddressId;
            preDelivery.Contact = _dd.UserName;
            preDelivery.ContactEmail = o.Email;
            var address = new Address();
            address.AddressLines = new List<string>();
            address.AddressLines.Add(_dd.CompanyName);
            address.AddressLines.Add(_dd.Line1);
            address.AddressLines.Add(_dd.Line2);
            address.City = _dd.City;
            address.Country = _dd.Country;
            address.Postcode = _dd.PostCode;
            address.Region = _dd.Region;

            preDelivery.Address = address;
            preDelivery.Collection = false;
            preDelivery.Quantity = tharEst.Quantity;
            preDelivery.Ref1 = o.RoiOrderId;
            preDelivery.SubJobs = null;

            var serializedResultJsonPreDelivery = JsonConvert.SerializeObject(
                                                    preDelivery,
                                                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            return serializedResultJsonPreDelivery;
        }


        public void SetBindingSide(PrintOptions x, int ProductTypeId, TharEstimateRequest.TharEstParts part)
        {
            //List<int> BoundProducts = new List<int>() { 19, 28, 29, 30, 37, 39, 40, 41 };

            //if (BoundProducts.IndexOf(ProductTypeId) != -1)
            //{

            TharEstimateRequest.tharEstBindingSide bindingSide = new TharEstimateRequest.tharEstBindingSide();

            if (x.Binding == "Top Edge")
                bindingSide.Value = 1; //Top
            else //horizontal spine
                bindingSide.Value = 0; //Top 

            part.BindingSide = bindingSide;
            //}

        }


        public void SetFoldingScheme(PrintOptions x, TharEstimateRequest.TharEstParts part)
        {
            //switch (Convert.ToInt32(x.PageCount))
            //{
            //    case 6:
            //        if (x.concertina == true)
            //            part.FoldingHeaderID = 5; //F6-2, 3 X 1 Concertina
            //        break;
            //    case 8:
            //        if (x.gateFold == true)
            //            part.FoldingHeaderID = 11; //"F8-1, 4 X 1"
            //        else if (x.flatPlan.ToLower().Contains("2 x 2"))
            //            part.FoldingHeaderID = 16; //F8-7, 2 X 2
            //        else if (x.flatPlan.ToLower().Contains("4 x 1"))
            //            part.FoldingHeaderID = 11; //"F8-1, 4 X 1
            //        //else if (x.flatPlan.ToLower().Contains(""))
            //        //{
            //        //    part.FoldingHeaderID = ; //
            //        //}
            //        break;
            //    case 10:
            //        if (x.flatPlan.ToLower().Contains("5 x 1"))
            //            part.FoldingHeaderID = 17; //F12-14, 2 X 3
            //        break;
            //    case 12:
            //        if (x.flatPlan.ToLower().Contains("2 x 3"))
            //            part.FoldingHeaderID = 33; //F12-14, 2 X 3
            //        else if (x.flatPlan.ToLower().Contains("6 x 1"))
            //            part.FoldingHeaderID = 20; //F12-1, 6 X 1
            //        else if (x.flatPlan.ToLower().Contains("3 x 2"))
            //            part.FoldingHeaderID = 29; //F12-10, 3 X 2
            //        break;

            //}



        }

        public void SetFoldingProcess(PrintOptions x, List<TharEstimateRequest.tharProcesses> listPartProcess)
        {
            //if (x.gateFold == true)
            //{
            //    TharEstimateRequest.tharProcesses gateFold = new TharEstimateRequest.tharProcesses();
            //    gateFold.ProcessTypeID = 8;
            //    gateFold.CostCentreID = 1274;
            //    gateFold.Value = 1;
            //    listPartProcess.Add(gateFold);
            //}
        }

        public string GetSpecificPress(PrintOptions x, string digiOrLitho, TharEstimateRequest.TharEstParts part)
        {
            string press = "";
            if (digiOrLitho == "litho")
            {

                int totalSpots = Convert.ToInt16(part.FrontSpots) + Convert.ToInt16(part.BackSpots);

                if (part.FrontProcess == "1" && totalSpots <= 1)
                    press = "PRO_SM102 2C";//
                else if ((part.FrontProcess == "1" && totalSpots > 1) || (part.FrontProcess == "4"))
                    if (part.PaperCode.Contains("SIL") || part.PaperCode.Contains("GLO"))
                        press = "PRO_XL106_5C";//
                    else
                        press = "PRO_XL105_4C";



                //          x.coloursOtherCoverage.ForEach(c =>
                //{
                //    if (c.Type == "cmyk")
                //    {
                //        if (c.Front == "1")
                //            press = "PRO_SM102 2C";//
                //        else if (c.Front == "4")
                //            if (x.paperType.Contains("Silk") || x.paperType.Contains("Gloss") || c.)
                //                press = "PRO_XL106_5C";//
                //            else
                //                press = "PRO_XL105_4C";
                //    }
                //});
            }

            return press;
        }

        public void SetDieCutProcess(PrintOptions x, List<TharEstimateRequest.tharProcesses> listPartProcess)
        {
            //if ((!string.IsNullOrEmpty(x.dieCut)))
            //{
            //    TharEstimateRequest.tharProcesses dieCut = new TharEstimateRequest.tharProcesses();
            //    dieCut.ProcessTypeID = 20;
            //    dieCut.CostCentreID = 1241;
            //    dieCut.MaterialCode = "FORME 01";

            //    listPartProcess.Add(dieCut);
            //}
        }

        public void GetQuantity(PrintOptions x, TharEstimateRequest tharEst)
        {
            if (x.Quantity.Contains("("))
            {
                var qtyArray = x.Quantity.Split(new char[] { '(' });

                var qty = qtyArray[0];

                tharEst.Quantity = Convert.ToInt32(qty);

                x.Quantity = qty;
            }
            else
            {
                tharEst.Quantity = Convert.ToInt32(x.Quantity);
            }
        }

        public bool CreateTharsternPushEntry(Order o, PrintOptions x)
        {
            long roiid = Convert.ToInt64(o.RoiOrderId);
            var order = _ctx.tOrders.FirstOrDefault(or => or.ROIOrderId == roiid);

            var tharsternPushDetails =
                _ctx.tOrderTharsternPushDetails.FirstOrDefault(pu => pu.OrderID == order.OrderID && pu.DocumentId == x.DocumentId);

            if (tharsternPushDetails == null)
            {

                if (order != null)
                    tharsternPushDetails = new tOrderTharsternPushDetails
                    {
                        DocumentId = x.DocumentId,
                        OrderID = order.OrderID
                    };
                _ctx.tOrderTharsternPushDetails.Add(tharsternPushDetails);
                _ctx.SaveChanges();
            }
            else
            {
                if (Convert.ToBoolean(tharsternPushDetails.PushedToTharstern))
                    return true;
                else
                {
                    tharsternPushDetails.Message = "";
                    tharsternPushDetails.TharsternJson = null;
                    tharsternPushDetails.Message = null;
                    _ctx.SaveChanges();
                }
            }

            return false;
        }

        public TharEstimateRequest.TharEstCustomer SetCustomer(Order o)
        {
            TharEstimateRequest.TharEstCustomer customer = new TharEstimateRequest.TharEstCustomer
            {
                Code = o.AccountCode,
                Contact = o.UserName,
                ContactEmail = o.Email
            };

            return customer;
        }

        public async Task SetBillingAndDeliveryAddress(Order o, TharEstimateRequest tharEst, string documentid, AddressDetails customerAddress)
        {
            TharEstimateRequest.TharEstCustomer deliveryCustomer = new TharEstimateRequest.TharEstCustomer();
            //CHECK ADDRESS EXISTS
            if (_dd.Line1 == null && _dd.PostCode == null)
            {
                Logger.WriteLog("No delivery address set in ROI - OrderId:" + o.RoiOrderId);
                UpdateLastMessage(Convert.ToInt64(o.RoiOrderId), "No delivery address set in ROI, please check order and contact customer.", documentid);
            }
            else
            {
                var newDeliveryCode = await CreateTharsternDeliveryAddress(o.AccountCode); // customer.Code.ToString()


                await CreateTharsternCustomerAddress(o.AccountCode, customerAddress);

                if (_engine.DeliveryAddressId > 0)
                {
                    Logger.WriteLog("Tharstern estimate Delivery Address creation- successful for PDQOrderId:" + o.RoiOrderId + "; Delivery Address Code:" + newDeliveryCode);
                    deliveryCustomer.Code = newDeliveryCode;
                    tharEst.DeliveryCustomer = deliveryCustomer;
                }
                else
                {
                    Logger.WriteLog("Tharstern estimate Delivery Address creation- failed for  PDQOrderId:" + o.RoiOrderId);
                    UpdateLastMessage(Convert.ToInt64(o.RoiOrderId), "Delivery Address creation- failed, please check order and update.", documentid);
                }
            }
        }

        public void SetCreasingProcessLeaflets(PrintOptions x, List<TharEstimateRequest.tharProcesses> listPartProcess, string digiOrLitho)
        {
            if ((!string.IsNullOrEmpty(digiOrLitho)) && Convert.ToInt32(x.Gsm) >= 170)
            {
                TharEstimateRequest.tharProcesses creasing = new TharEstimateRequest.tharProcesses();

                if (digiOrLitho == "litho")
                {
                    creasing.ProcessTypeID = 20;
                    creasing.CostCentreID = 1240;
                    creasing.MaterialCode = "FORME CREASE";
                }
                else if (digiOrLitho == "digital")
                {
                    creasing.ProcessTypeID = 20;
                    creasing.CostCentreID = 1257;
                    //creasing.MaterialCode = "FORME 01";
                }
                listPartProcess.Add(creasing);
            }
        }

        public async Task SubmitNotes(Order o, string notes)
        {
            if (!string.IsNullOrEmpty(notes))
            {
                TharNote.RootObject rootObjNote = new TharNote.RootObject();
                TharNote.ModuleType noteModuleType = new TharNote.ModuleType();

                noteModuleType.Value = 1; // 1 = estimate || 2 = job
                rootObjNote.IsPriority = true;
                rootObjNote.ModuleNo = _engine.EstimateNo;
                rootObjNote.ModuleType = noteModuleType;
                rootObjNote.Notes = notes;
                rootObjNote.UserId = 129;

                var serializedNoteResultJson = JsonConvert.SerializeObject(
                  rootObjNote,
                  new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                // Logger.WriteLog("Tharstern Notes submission json for  PDQOrderId:" + o.ROIOrderId + " : " + serializedNoteResultJson);

                await _engine.GetObjectFromPostAsync("notes", serializedNoteResultJson);

            }
        }

        public bool DownloadPdf(string url, string filename)
        {
            bool success = true;

            try
            {
                WebClient webClient = new WebClient();
                webClient.DownloadFile(url, filename);

            }
            catch (Exception ex)
            {
                success = false;
            }
            return success;
        }

        public async Task SubmitPOandArtwork(Order o, string po, string artwork, string prodName, string documentid)
        {
            var moduleType = new TharAttachment.ModuleType();
            var typeArtwork = new TharAttachment.Type();
            var typePO = new TharAttachment.Type();
            var itemArtwork = new TharAttachment.Item();
            var itemPO = new TharAttachment.Item();
            var rootObj = new TharAttachment.RootObject();
            rootObj.Items = new List<TharAttachment.Item>();

            moduleType.Value = 1; // 1= estimate, 2= job
            typeArtwork.Value = 10; //0 = document, 10 = all content
            typePO.Value = 20;

            if (!string.IsNullOrEmpty(artwork))
            {
                var filename = System.IO.Path.GetFileName(artwork);
                itemArtwork.Filename = filename;
                itemArtwork.ModuleType = moduleType;
                itemArtwork.RecordNo = _engine.EstimateNo.Trim();
                itemArtwork.Type = typeArtwork;
                itemArtwork.Details = "Artwork";
                itemArtwork.Content_URL = artwork;
                rootObj.Items.Add(itemArtwork);

            }

            if (!string.IsNullOrEmpty(po))
            {
                Document doc = new Document();

                //Create a New instance of PDFWriter Class for Output File

                var rootPath = ConfigurationManager.AppSettings["POSavePath"];

                PdfWriter.GetInstance(doc, new FileStream(rootPath + "//" + o.RoiOrderId + "_po.pdf", FileMode.Create));

                //Open the Document

                doc.Open();

                //Add the content of Text File to PDF File

                doc.Add(new Paragraph("PO Number: " + po));

                //Close the Document

                doc.Close();

                itemPO.Filename = o.RoiOrderId + "_po.pdf";
                itemPO.ModuleType = moduleType;
                itemPO.RecordNo = _engine.EstimateNo.Trim();
                itemPO.Type = typePO;
                itemPO.Details = "PO";
                itemPO.Content_URL = @"http://roi.approve4print.co.uk/" + o.RoiOrderId + "_po.pdf";
                rootObj.Items.Add(itemPO);
            }

            if (!string.IsNullOrEmpty(po) || !string.IsNullOrEmpty(artwork))
            {
                var serializedAttchmentResultJson = JsonConvert.SerializeObject(
                   rootObj,
                   new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                //Logger.WriteLog("Tharstern attchment submission json for  PDQOrderId:" + o.ROIOrderId + " : " + serializedAttchmentResultJson);

                await _engine.GetObjectFromPostAsync("attachments", serializedAttchmentResultJson);
            }
            else
            {
                if (string.IsNullOrEmpty(po))
                {
                    // Logger.WriteLog("Tharstern PO attchment submission not found for  PDQOrderId:" + o.ROIOrderId);
                    AddToLastMessage(Convert.ToInt64(o.RoiOrderId), "Tharstern PO attchment submission not found.", documentid);
                }
                if (string.IsNullOrEmpty(artwork))
                {
                    //Logger.WriteLog("Tharstern ARTWORK attchment submission not found for  PDQOrderId:" + o.ROIOrderId);
                    AddToLastMessage(Convert.ToInt64(o.RoiOrderId), "Tharstern ARTWORK attchment submission not found.", documentid);
                }
            }

        }

        public void SetOrientation(PrintOptions printOp, TharEstimateRequest tharEst)
        {
            TharEstimateRequest.tharOrientation tharEstOrientation = new TharEstimateRequest.tharOrientation();
            tharEstOrientation.EnumType = "OrientationEnum";

            if (!string.IsNullOrEmpty(printOp.Orientation))
            {
                if (printOp.Orientation.ToLower() == "landscape")
                    tharEstOrientation.Value = 1;
                else
                    tharEstOrientation.Value = 0; //portrait

                tharEst.Orientation = tharEstOrientation;
            }
        }

        public void SetCoatings(string digiOrLitho, PrintOptions x, TharEstimateRequest.TharEstParts part)
        {
            if (digiOrLitho == "litho" && (x.Stock.Contains("Silk") || x.Stock.Contains("Gloss")))
            {
                part.Coatings = "1";
                part.Seal1Type = "2"; //overall
                // Type values are:
                //1: Spot
                //2: Overall
                //3: Fit


                if (x.ColoursBack == "0")
                    part.Seal1Side = "1"; //side 1
                else
                    part.Seal1Side = "0"; //both


                //NOT SURE HOW TO IMPLEMENT ONLY SIDE 2 

                //Side values are:
                //0: Both
                //1: Side 1
                //2: Side 2
            }
        }

        public void SetLamination(PrintOptions x, List<TharEstimateRequest.tharProcesses> listPartProcess, string digiOrLitho)
        {

            if (string.IsNullOrEmpty(x.Lamination))
                return;

            if (x.Lamination.Contains("Please choose from list"))
                x.Lamination = "";

            if (x.Lamination == "Un-laminated")
                x.Lamination = "";

            if (!string.IsNullOrEmpty(x.Lamination))
            {
                var lam = x.Lamination.ToUpper();

                if (lam.Contains("UNL"))
                    x.Lamination = "";
            }

            if ((!string.IsNullOrEmpty(x.Lamination)))
            {
                TharEstimateRequest.tharProcesses lamination = new TharEstimateRequest.tharProcesses
                {
                    ProcessTypeID = 12
                };

                if (Convert.ToInt32(x.Gsm) >= 200)
                {
                    //200 and over lam in house
                    if (digiOrLitho == "litho")
                    {
                        if (x.Lamination.Contains("Matt Lam"))
                        {
                            lamination.CostCentreID = 1247;
                        }
                        if (x.Lamination.Contains("Gloss Lam"))
                        {
                            lamination.CostCentreID = 1246;
                        }

                        //If lamination comes with Code
                        if (x.Lamination.Contains("MLO") || x.Lamination.Contains("MLI") || x.Lamination.Contains("MLT"))
                            lamination.CostCentreID = 1247;

                        if (x.Lamination.Contains("GLO") || x.Lamination.Contains("GLI") || x.Lamination.Contains("GLT"))
                            lamination.CostCentreID = 1246;

                    }
                    else
                    {
                        if (x.Lamination.Contains("Matt Lam"))
                        {
                            lamination.CostCentreID = 1283;
                        }
                        if (x.Lamination.Contains("Gloss Lam"))
                        {
                            lamination.CostCentreID = 1282;
                        }
                        if (x.Lamination.Contains("laminate soft touch"))
                        {
                            lamination.CostCentreID = 1284;
                        }
                        //If lamination comes with Code
                        if (x.Lamination.Contains("MLO") || x.Lamination.Contains("MLI") || x.Lamination.Contains("MLT"))
                            lamination.CostCentreID = 1283;

                        if (x.Lamination.Contains("GLO") || x.Lamination.Contains("GLI") || x.Lamination.Contains("GLT"))
                            lamination.CostCentreID = 1282;

                    }
                    if (x.Lamination.Contains("both sides"))
                        lamination.Value = 1;
                    else
                        lamination.Value = 0;
                }
                else
                {
                    //under 200 LAM outside
                    if (x.Lamination.Contains("laminate matt"))
                    {
                        if (x.Lamination.Contains("both sides"))
                            lamination.OutworkID = 421; //Matt Laminate Double Sided
                        else
                            lamination.OutworkID = 420; //Matt Laminate single sided
                    }
                    if (x.Lamination.Contains("laminate gloss"))
                    {
                        if (x.Lamination.Contains("2 sides"))
                            lamination.OutworkID = 394; //Gloss Laminate Double Sided
                        else
                            lamination.OutworkID = 393; //Gloss Laminate single sided
                    }
                    if (x.Lamination.Contains("laminate soft touch"))
                    {
                        if (x.Lamination.Contains("2 sides"))
                            lamination.OutworkID = 423; //Soft Touch Laminate Double Sided
                        else
                            lamination.OutworkID = 422; //Soft Touch Laminate single sided
                    }


                    //If lamination comes with Code
                    if (x.Lamination.Contains("MLO") || x.Lamination.Contains("MLI"))
                        lamination.OutworkID = 420;

                    if (x.Lamination.Contains("MLT"))
                        lamination.OutworkID = 421;

                    if (x.Lamination.Contains("GLO") || x.Lamination.Contains("GLI"))
                        lamination.OutworkID = 393;

                    if (x.Lamination.Contains("GLT"))
                        lamination.OutworkID = 394;

                }
                listPartProcess.Add(lamination);
            }
        }

        public void SetDrillProcess(List<TharEstimateRequest.tharProcesses> listProcess, string binding)
        {
            //DRILL PROCESS 
            if (!string.IsNullOrEmpty(binding))
            {
                TharEstimateRequest.tharProcesses drillProcesses = new TharEstimateRequest.tharProcesses();
                if (binding == "drill")
                {
                    drillProcesses.ProcessTypeID = 29;
                    drillProcesses.CostCentreID = 1255;
                    drillProcesses.Value = 1;
                }
                listProcess.Add(drillProcesses);
            }
        }

        public void SetColoursAndOtherCoverage(PrintOptions x, TharEstimateRequest.TharEstParts part)
        {
            part.FrontProcess = x.ColoursFront;
            part.BackProcess = x.ColoursBack;

            //TODO: TO check later
            //no colours - treat as 4/4
            if (string.IsNullOrEmpty(part.FrontProcess) && string.IsNullOrEmpty(part.BackProcess))
            {
                part.FrontProcess = "4";
                part.BackProcess = "4";
            }

            if (x.ColoursFront == x.ColoursBack)
                part.SameColours = true;
        }

        public TharEstimateRequest.tharProcesses SetPackingProcess(PrintOptions printOp)
        {
            //PACKING PROCESS
            int width, height = 0;
            int.TryParse(printOp.Height, out height);
            int.TryParse(printOp.Width, out width);

            TharEstimateRequest.tharProcesses packingProcesses = new TharEstimateRequest.tharProcesses();

            if (width <= 297 && height <= 420)
            {
                //CARTON A3 or less
                packingProcesses.CostCentreID = 1236;
                packingProcesses.ProcessTypeID = 13;
            }
            else if (width > 297 && height > 420 && Convert.ToInt32(printOp.Quantity) <= 10000)
            {
                //TUBE Greater than A3 QUANTITY LESS THAN ???? TODO
                packingProcesses.CostCentreID = 1278;
                packingProcesses.ProcessTypeID = 13;
            }
            else
            {
                //PALLETISE
                packingProcesses.ProcessTypeID = 43;
                packingProcesses.OutworkID = 453;
            }

            packingProcesses.Value = Convert.ToInt32(printOp.Quantity);
            return packingProcesses;
        }

        public void SetSize(TharEstimateRequest.tharEstFinishedSize estFinishedSize, PrintOptions printOp, TharEstimateRequest tharEst)
        {
            FinalSizeMapping sizeMapping = null;

            if (!string.IsNullOrEmpty(printOp.Orientation))
            {
                var x = printOp.Height;
                var y = printOp.Width;

                var max = 0;
                var min = 0;
                var width = string.Empty;
                var height = string.Empty;

                if (!string.IsNullOrEmpty(printOp.Orientation))
                {
                    if (printOp.Orientation.ToLower() == "landscape")
                    {
                        max = Math.Max(Convert.ToInt32(y), Convert.ToInt32(x));
                        width = max.ToString();

                        min = Math.Min(Convert.ToInt32(y), Convert.ToInt32(x));
                        height = min.ToString();
                    }
                    else//Portrait
                    {
                        max = Math.Max(Convert.ToInt32(y), Convert.ToInt32(x));
                        height = max.ToString();

                        min = Math.Min(Convert.ToInt32(y), Convert.ToInt32(x));
                        width = min.ToString();
                    }

                    printOp.Height = height;
                    printOp.Width = width;
                }

            }


            if (printOp.ProductName.ToLower().Contains("Folder") && printOp.ProductType == "Capacity")
                sizeMapping = _ctx.FinalSizeMapping.FirstOrDefault(s => s.FinalSizeHeight == printOp.Height && s.FinalSizeWidth == printOp.Width && s.Size.Contains("5MM CAPACITY"));
            else if (printOp.ProductName.ToLower().Contains("Folder") && printOp.ProductType != "Capacity")
                sizeMapping = _ctx.FinalSizeMapping.FirstOrDefault(s => s.FinalSizeHeight == printOp.Height && s.FinalSizeWidth == printOp.Width && s.Size.Contains("NON CAPACITY"));
            else
                sizeMapping = _ctx.FinalSizeMapping.FirstOrDefault(s => s.FinalSizeHeight == printOp.Height && s.FinalSizeWidth == printOp.Width);


            if (sizeMapping != null)
            {
                estFinishedSize.Code = sizeMapping.Size;
            }
            else
            {
                estFinishedSize.Depth = Convert.ToInt32(printOp.Height);
                estFinishedSize.Width = Convert.ToInt32(printOp.Width);
            }
            tharEst.FinishedSize = estFinishedSize;
        }

        public string SetJobType(string digiOrLitho)
        {
            return digiOrLitho == "litho" ? "KF-ROI-LITHO-PRODUCTS" : "KF-ROI-DIGITAL-PRODUCTS";
        }

        public string SetDate(string digiOrLitho, DateTime orderDate)
        {
            DateTime deliveryDate;
            if (digiOrLitho == "digital")
            {
                if (orderDate.Hour < 13)
                    deliveryDate = DateTime.Now.AddDays(1); //2 days before 1pm
                else
                    deliveryDate = DateTime.Now.AddDays(2);//3 days after 1pm
            }
            else //LITHO
            {
                //if (orderDate.Hour < 13)
                //    deliveryDate = DateTime.Now.AddDays(4); //2 days before 1pm
                //else
                deliveryDate = DateTime.Now.AddDays(5);//3 days after 1pm
            }

            if (deliveryDate.DayOfWeek == DayOfWeek.Saturday)
            {
                deliveryDate = deliveryDate.AddDays(2);
            }

            if (deliveryDate.DayOfWeek == DayOfWeek.Sunday)
            {
                deliveryDate = deliveryDate.AddDays(1);
            }

            return deliveryDate.ToString("dd-MMM-yy");
        }

        public void DumpTharsternJson(string orderId, string serializedResultJson, string documentid)
        {
            var oid = Convert.ToInt64(orderId);

            var order = _ctx.tOrders.FirstOrDefault(or => or.ROIOrderId == oid);

            if (order != null)
            {
                var orderTharsternPush = _ctx.tOrderTharsternPushDetails.FirstOrDefault(o => o.OrderID == order.OrderID && o.DocumentId == documentid);

                if (orderTharsternPush != null)
                {
                    orderTharsternPush.TharsternJson = serializedResultJson;
                    _ctx.SaveChanges();
                }

            }
        }

        public string GetDigiorLithoFromPrintMethod(string orderId, string width, string height, int pageCount, TharEstimateRequest.tharEstFinishedSize estFinishedSize, PrintOptions printOp)
        {
            //NEED TO HANDLE BEST METHOD
            string digiOrLitho = "";
            int qty = Convert.ToInt32(printOp.Quantity);
            bool changedToLitho = false;
            string reason = "";
            //GET PRINTING METHOD
            if (!string.IsNullOrEmpty(printOp.PrintMethod))
            {
                if (printOp.PrintMethod.ToLower().Contains("digital"))
                    digiOrLitho = "digital";
                else if (printOp.PrintMethod.ToLower().Contains("litho"))
                    digiOrLitho = "litho";
            }


            if (digiOrLitho == "digital")
            {
                //TODO: SORT THIS SHIT OUT - business cards
                // equal to or greater then 8 pp and width less or equal to 148 and height less or equal to 210
                if ((Convert.ToInt32(printOp.PageCount) > 8 && Convert.ToInt32(width) <= 148 &&
                     Convert.ToInt32(height) <= 210 && qty >= 400) || (Convert.ToInt32(printOp.PageCount) > 8 &&
                                                                       Convert.ToInt32(width) <= 210 &&
                                                                       Convert.ToInt32(height) <= 148 && qty >= 400))
                {
                    changedToLitho = true;
                    reason = "Greater or equal to A5 and greater then 8pp and quantity greater or equal to 400";
                }
                else if (printOp.ProductType.ToLower().Contains("Card") && qty >= 5000) // Business cards
                {
                    changedToLitho = true;
                    reason = "Business card with a quantity greater or equal to 5000";
                }
                else if (printOp.ProductType.Contains("Leaflet Unfolded") && pageCount == 2 && qty >= 1000
                ) // Leaflets 2pp
                {
                    changedToLitho = true;
                    reason = "Leaflet 2pp  with a quantity greater or equal to 1000";
                }
                else if ((printOp.ProductType.Contains("FoldedSectionVerticalSpine")) && (Convert.ToInt32(width) * Convert.ToInt32(height) <= 15662)) //slightly larger than  A6
                {
                    changedToLitho = true;
                    reason = "A6 or less, stitched work can only be fulfilled litho";
                }
                else if (printOp.ProductType.Contains("Leaflet Folded") && pageCount > 2 &&
                         !string.IsNullOrEmpty(estFinishedSize.Code)) //Leaflets folded
                {
                    if (qty >= 500 && estFinishedSize.Code.Contains("A4"))
                    {
                        changedToLitho = true;
                        reason = "Leaflet folded A4 with a quantity greater or equal to 500";
                    }

                    if (qty >= 1000 && estFinishedSize.Code.Contains("A5"))
                    {
                        changedToLitho = true;
                        reason = "Leaflet folded A5 with a quantity greater or equal to 1000";
                    }

                    if (qty >= 2000 && (estFinishedSize.Code == "A6" ||
                                        estFinishedSize.Code == "A7" && estFinishedSize.Code == "A8"))
                    {
                        changedToLitho = true;
                        reason = "Leaflet folded " + estFinishedSize.Code + " with a quantity greater or equal to 2000";
                    }
                }
                else if (printOp.ProductType.Contains("SS Self Cover") && qty >= 500) //Stitched Self Cover
                {
                    changedToLitho = true;
                    reason = "Stitched self cover with a quantity greater or equal to 500";
                }
                else if (printOp.ProductType.Contains("SS With Cover") && qty >= 500) //Stitched With Cover
                {
                    changedToLitho = true;
                    reason = "Stitched with cover with a quantity greater or equal to 500";
                }
                else if (printOp.ProductType.Contains("Perfect Bound") && qty >= 400) //PUR Bound
                {
                    changedToLitho = true;
                    reason = "PB with a quantity greater or equal to 400";
                }
            }
            else
            {
                Logger.WriteLog("Digi or Litho not determined for Print method" + printOp.PrintMethod + "; OrderId:" + orderId);
                AddToLastMessage(Convert.ToInt64(orderId), "Digi or Litho not determined for Print method.", printOp.DocumentId);
            }

            if (changedToLitho)
            {
                digiOrLitho = "litho";
                if (printOp.PrintMethod.ToLower().Contains("digital"))
                {
                    AddToLastMessage(Convert.ToInt64(orderId), "Job changed to LITHO as not possible to fulfill on Digi. Reason- " + reason, printOp.DocumentId);
                    Logger.WriteLog("Job changed to LITHO as not possible to fulfill on Digi - OrderId: " + orderId);
                }
            }

            return digiOrLitho;
        }

        public bool IsValidProductType(List<TharEstimateRequest.TharEstParts> listParts)
        {
            bool valid = true;

            foreach (var specs in listParts)
            {
                if (specs.ProductTypePartId == 0)
                {
                    valid = false;
                    break;
                }
            }
            return valid;
        }

        public bool IsValidPaperCode(List<TharEstimateRequest.TharEstParts> listParts)
        {
            bool valid = true;

            foreach (var specs in listParts)
            {
                if (string.IsNullOrEmpty(specs.PaperCode))
                {
                    valid = false;
                    break;
                }
            }
            return valid;
        }

        public void UpdateLastMessage(long orderId, string lastMessage, string documentid)
        {
            _ctx = new ROIEntities();

            var order = _ctx.tOrders.Where((o => o.ROIOrderId == orderId)).FirstOrDefault();

            if (order != null)
            {
                var tharsternPushDetails = _ctx.tOrderTharsternPushDetails.FirstOrDefault(o => o.OrderID == order.OrderID && o.DocumentId == documentid);

                if (tharsternPushDetails != null)
                {
                    tharsternPushDetails.Message = lastMessage;
                    _ctx.SaveChanges();

                }
            }

        }

        public void AddToLastMessage(long orderId, string lastMessage, string documentid)
        {
            _ctx = new ROIEntities();
            var order = _ctx.tOrders.Where((o => o.ROIOrderId == orderId)).FirstOrDefault();

            if (order != null)
            {
                var tharsternPushDetails = _ctx.tOrderTharsternPushDetails.FirstOrDefault(o => o.OrderID == order.OrderID && o.DocumentId == documentid);
                if (tharsternPushDetails != null)
                {
                    tharsternPushDetails.Message += " --- " + lastMessage;
                    _ctx.SaveChanges();
                }
            }

        }

        public void MarkTharsternPushFailure(string roiorderId)
        {
            var roiId = Convert.ToInt64(roiorderId);
            var order = _ctx.tOrders.FirstOrDefault(o => o.ROIOrderId == roiId);

            if (order != null)
            {
                order.FailedInProcessing = true;
                _ctx.SaveChanges();
            }

        }


        public void UpdateJobDelNote(string documentId, string DelNote)
        {
            var data = _ctx.tOrderTharsternPushDetails.FirstOrDefault(or => or.DocumentId == documentId);

            if (data != null)
            {
                data.JobDeliveryNote = DelNote;
                _ctx.SaveChanges();
            }

        }


        public void UpdateCustomerDelNote(string documentId, string DelNote)
        {
            var data = _ctx.tOrderTharsternPushDetails.FirstOrDefault(or => or.DocumentId == documentId);

            if (data != null)
            {
                data.CustomerDeliveryNote = DelNote;
                _ctx.SaveChanges();
            }

        }


        public void MarkTharsternPushSuccess(string roiorderId, string estRef, string json, string documentId)
        {
            var data = _ctx.tOrderTharsternPushDetails.FirstOrDefault(or => or.DocumentId == documentId);

            if (data == null)
            {
                data = new tOrderTharsternPushDetails { DocumentId = documentId };

                var roiId = Convert.ToInt64(roiorderId);
                var order = _ctx.tOrders.FirstOrDefault(o => o.ROIOrderId == roiId);

                if (order != null) data.OrderID = order.OrderID;
            }

            data.PushedToTharstern = true;

            string estno = !string.IsNullOrEmpty(estRef) ? estRef : _engine.EstimateNo;

            data.TharsternEstiamteNo = estno;

            data.TharsternPushDatetime = DateTime.Now;
            data.TharsternJson = json;

            _ctx.SaveChanges();
        }

        public async Task<string> CreateTharsternDeliveryAddress(string customerCode)
        {
            //create delivery address using API and assign code 
            TharDeliveryAddress.TharDeliveryAddressDetails addressDetails =
                new TharDeliveryAddress.TharDeliveryAddressDetails
                {
                    AddressLines = new[] { _dd.Line1, _dd.Line2 },
                    City = _dd.City,
                    Country = _dd.Country,
                    Postcode = _dd.PostCode,
                    Region = _dd.Region
                };


            TharDeliveryAddress deliveryAddress = new TharDeliveryAddress();
            deliveryAddress.CustomerCode = customerCode;
            deliveryAddress.Name = _dd.UserName;
            var newDeliveryCode = deliveryAddress.CustomerCode + " " + new Random().Next(10000000, 99999999);
            deliveryAddress.Code = newDeliveryCode;
            deliveryAddress.AddressDetails = addressDetails;

            var serializedResultDeliveryJson = JsonConvert.SerializeObject(
                deliveryAddress,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            _engine.DeliveryRequest = serializedResultDeliveryJson;

            await _engine.CreateDeliveryAsync();
            return newDeliveryCode;
        }

        public async Task<string> CreateTharsternCustomerAddress(string customerCode, AddressDetails customerAddress)
        {
            //create delivery address using API and assign code 
            TharDeliveryAddress.TharDeliveryAddressDetails addressDetails =
                new TharDeliveryAddress.TharDeliveryAddressDetails
                {
                    AddressLines = new[] { customerAddress.Line1, customerAddress.Line2 },
                    City = customerAddress.City,
                    Country = customerAddress.Country,
                    Postcode = customerAddress.PostCode,
                    //Region = customerAddress.Region
                };


            TharDeliveryAddress customerTharAddress = new TharDeliveryAddress();
            customerTharAddress.CustomerCode = customerCode;
            customerTharAddress.Name = customerAddress.FirstName + " " + customerAddress.LastName;
            var newDeliveryCode = customerTharAddress.CustomerCode + " " + new Random().Next(10000000, 99999999);
            customerTharAddress.Code = newDeliveryCode;
            customerTharAddress.AddressDetails = addressDetails;

            var serializedResultCustomerAddress = JsonConvert.SerializeObject(
                customerTharAddress,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            _engine.CustomerAddressRequest = serializedResultCustomerAddress;

            await _engine.CreateCustomerAddressAsync();
            return newDeliveryCode;
        }

        public int SetProductTypeId(int pageCount, string orderId, string digiorLitho, string width, string height, PrintOptions printOp)
        {
            int pid = 0;

            if (digiorLitho == "litho")
            {
                if (printOp.ProductType.ToLower().Contains("wiro"))
                    pid = 41;

                //MATCH THE SECTION NAMES TO THE ONES IN THARSTERNS
                if (printOp.ProductType.Contains("Perfect Bound"))
                    pid = 40; //05 PUR Bound
                else if (printOp.ProductType.Contains("SS With Cover"))
                    pid = 39; //04 Stitched With Cover
                else if (printOp.ProductType.Contains("Folders"))
                    pid = 14; //06 Folders-Flat Size
                else if (printOp.ProductType.Contains("Poster") && pageCount == 2)
                    pid = 35; //01 Leaflets - 2pp 
                else if (printOp.ProductType.Contains("Leaflet Unfolded") && pageCount == 2)
                    pid = 35; //01 Leaflets - 2pp 
                else if (printOp.ProductType.Contains("Business Cards"))
                    pid = 35; //01 Leaflets - 2pp 
                else if (printOp.ProductType.Contains("Leaflet Folded") && pageCount > 2)
                    pid = 36; //02 Leaflets - Folded
                else if (printOp.ProductType.Contains("SS Self Cover"))
                    pid = 37; //03 Stitched Self Cover
            }
            else
            {
                if (printOp.ProductType.ToLower().Contains("wiro"))
                    pid = 19;

                //MATCH THE SECTION NAMES TO THE ONES IN THARSTERNS
                if (printOp.ProductType.Contains("Perfect Bound"))
                    pid = 30; //17 PUR Bound - Digital

                else if (printOp.ProductType.Contains("SS With Cover"))
                    pid = 29; //16 Stitched With Cover - Digital
                else if ((printOp.ProductType.Contains("Poster")) && (Convert.ToInt32(width) > 297 && Convert.ToInt32(height) > 420) || Convert.ToInt32(height) > 297 && Convert.ToInt32(width) > 420)
                    pid = 18; //07 Wide Format
                else if (printOp.ProductType.Contains("Business Cards"))
                    pid = 23; //12 Digital Business Cards
                else if (printOp.ProductType.Contains("Leaflet Unfolded") && pageCount == 2)
                    pid = 26; //13 Leaflets - 2pp Digital
                else if ((printOp.ProductType.Contains("Leaflet Folded") || printOp.ProductType.Contains("Leflet Folded")) && pageCount > 2)
                    pid = 27; //14 Leaflets - Folded Digital

                else if (printOp.ProductType.Contains("SS Self Cover"))
                    pid = 28; //15 Stitched Self Cover - Digital
                //TODO: needs to be added to Tharstern 

                else if (printOp.ProductType.Contains("UnfoldedItem"))
                    pid = 22; //11 Digital Stationery
            }


            if (pid == 0)
            {
                Logger.WriteLog("No product type id found for product type:" + printOp.ProductType +
                                          ", PageCount: " + pageCount + ". OrderId:" + orderId);
                UpdateLastMessage(Convert.ToInt64(orderId), "FAILED - No product type id found for product type:" + printOp.ProductType +
                                          ", PageCount: " + pageCount, printOp.DocumentId);
            }

            return pid;

        }

        public int GetProductTypePartId(string sectionName, string printType, string orderId, int prodTypeId, string documentid)
        {
            int prodTypePartId = 0;

            switch (prodTypeId)
            {
                ////DIGITAL////
                case 19://WIROBOUND
                    if (sectionName.Contains("cover"))
                        prodTypePartId = 28;
                    else if (sectionName.Contains("inner leaves"))
                        prodTypePartId = 29;
                    else if (sectionName.Contains("insert"))
                        prodTypePartId = 30;
                    break;

                case 28://SELF COVER
                    if (sectionName.Contains("text") || sectionName.Contains("self cover"))
                        prodTypePartId = 41;
                    break;

                case 29://WITH COVER
                    if (sectionName.Contains("cover"))
                        prodTypePartId = 42;
                    else if (sectionName.Contains("text"))
                        prodTypePartId = 43;
                    else if (sectionName.Contains("insert"))
                        prodTypePartId = 44;
                    break;

                case 26://LEAFLET 2pp
                    prodTypePartId = 39;
                    break;

                case 27://LEAFLET folded
                    prodTypePartId = 40;
                    break;

                case 30://PURBOUND
                    if (sectionName.Contains("cover"))
                        prodTypePartId = 45;
                    else if (sectionName.Contains("text"))
                        prodTypePartId = 46;
                    else if (sectionName.Contains("insert"))
                        prodTypePartId = 47;
                    break;

                case 22://stationery
                    prodTypePartId = 35;
                    break;

                case 23://stationery
                    prodTypePartId = 36;
                    break;

                case 18: // wide format
                    prodTypePartId = 27;
                    break;

                ////LITHO////

                case 41://WIROBOUND
                    if (sectionName.Contains("cover"))
                        prodTypePartId = 72;
                    else if (sectionName.Contains("inner leaves"))
                        prodTypePartId = 73;
                    else if (sectionName.Contains("insert"))
                        prodTypePartId = 74;
                    break;

                case 37://SELF COVER
                    if (sectionName.Contains("text") || sectionName.Contains("self cover"))
                        prodTypePartId = 61;
                    else if (sectionName.Contains("insert"))
                        prodTypePartId = 62;
                    break;

                case 39://WITH COVER
                    if (sectionName.Contains("cover"))
                        prodTypePartId = 66;
                    else if (sectionName.Contains("text"))
                        prodTypePartId = 67;
                    else if (sectionName.Contains("insert"))
                        prodTypePartId = 68;
                    break;

                case 35://LEAFLET 2pp
                    prodTypePartId = 58;
                    break;

                case 36://LEAFLET folded
                    if (sectionName.Contains("insert"))
                        prodTypePartId = 60;
                    else
                        prodTypePartId = 59;
                    break;

                case 40://PURBOUND
                    if (sectionName.Contains("cover"))
                        prodTypePartId = 69;
                    else if (sectionName.Contains("text"))
                        prodTypePartId = 70;
                    else if (sectionName.Contains("insert"))
                        prodTypePartId = 71;
                    break;

                case 24://Roller Banners
                    prodTypePartId = 37;
                    break;

                case 14://06 Folders-Flat Size
                    prodTypePartId = 23;
                    break;

                case 21:
                    prodTypePartId = 34;
                    break;
            }

            if (prodTypePartId == 0)
            {
                Logger.WriteLog("No product type part id found for :" + sectionName +
                                          "-" + prodTypeId + "-" + printType + ". OrderId: " + orderId);
                UpdateLastMessage(Convert.ToInt64(orderId), "No product type part id found for :" + sectionName +
                                          "-" + prodTypeId + "-" + printType, documentid);
            }
            return prodTypePartId;
        }

        public string GetPaperCode(PrintOptions specification, string orderId, string digiOrLitho, string partType, string documentid)
        {
            string type = "ESP";

            if (partType == "Cover")
            {
                string stock = specification.CoverStock;
                stock = stock.Trim();
                string gsm = specification.CoverGsm;

                //if (stock == "laser guaranteed uncoated" && gsm == "120")
                //{
                //    return "NAV001-2";
                //}

                string paper = GetPaper(stock);
                string newGsm = SetGsm(orderId, digiOrLitho, gsm, paper, documentid);

                if (!string.IsNullOrEmpty(newGsm))
                {
                    specification.CoverGsm = newGsm;
                }

                if (string.IsNullOrEmpty(paper))
                {
                    Logger.WriteLog("No paper found for paper type: " + stock + " Order Id: " + orderId);
                    UpdateLastMessage(Convert.ToInt64(orderId), "No paper found for paper type: " + stock, documentid);
                    return string.Empty;
                }

                specification.CoverGsm = specification.CoverGsm.PadLeft(3, '0');

                if (stock == "INVERCOTE CREATO")
                {
                    return paper + specification.CoverGsm;
                }

                if (paper == "VISION")
                    return paper + specification.CoverGsm;
                return type + paper + specification.CoverGsm;
            }
            else
            {
                string stock = specification.Stock;
                stock = stock.Trim();
                string gsm = specification.Gsm.Replace("GSM", "");


                //if (stock == "laser guaranteed uncoated" && gsm == "120")
                //{
                //    return "NAV001-2";
                //}
                string paper = GetPaper(stock);
                string newGsm = SetGsm(orderId, digiOrLitho, gsm, paper, documentid);

                if (!string.IsNullOrEmpty(newGsm))
                {
                    specification.Gsm = newGsm;
                }

                if (string.IsNullOrEmpty(paper))
                {
                    Logger.WriteLog("No paper found for paper type: " + stock + " Order Id: " + orderId);
                    UpdateLastMessage(Convert.ToInt64(orderId), "No paper found for paper type: " + stock, documentid);
                    return string.Empty;
                }

                specification.Gsm = specification.Gsm.PadLeft(3, '0');

                if (stock == "INVERCOTE CREATO")
                {
                    return paper + specification.Gsm;
                }

                if (paper == "VISION")
                    return paper + specification.Gsm;

                return type + paper + specification.Gsm;
            }
        }

        public string SetGsm(string orderId, string digiOrLitho, string gsm, string paper, string documentid)
        {
            string newGsm = "";
            if (gsm == "200" && paper == "OFFSET")
            {
                AddToLastMessage(Convert.ToInt64(orderId), "PAPER GSM CHANGED FROM 200 to 190 AS NOT STOCKED", documentid);
                newGsm = "190";
            }
            else if (digiOrLitho == "digital" && Convert.ToInt32(gsm) < 100 && paper == "SIL")
            {
                AddToLastMessage(Convert.ToInt64(orderId), "PAPER CHANGED FROM " + gsm + " TO 100GSM TO BE ABLE TO PRINT DIGI", documentid);
                newGsm = "100";
            }
            else if (digiOrLitho == "digital" && Convert.ToInt32(gsm) < 90 && (paper == "RECOFF" || paper == "OFFSET"))
            {
                AddToLastMessage(Convert.ToInt64(orderId), "PAPER CHANGED FROM " + gsm + " TO 90GSM TO BE ABLE TO PRINT DIGI", documentid);
                newGsm = "90";
            }

            if (string.IsNullOrEmpty(newGsm))
                newGsm = gsm;

            return newGsm;
        }

        public string GetPaper(string stock)
        {
            stock = stock.Trim();

            string paper = "";


            if (stock.Contains("gsm"))
                stock = stock.Replace("gsm", "");

            stock = stock.Trim();

            if (stock.ToUpper() == "REGENCY SATIN")
                return paper = "SIL";

            if (stock == "VISION SUPERIOR")
            {
                return "VISION";
            }

            if (stock.ToUpper().Contains("LASER GUARANTEED UNCOATED"))
            {
                return "PREPRI";
            }

            if (stock == "INVERCOTE CREATO")
            {
                return "INVCREATO";
            }

            if (stock == "REVIVE 100" || stock == "REVIVE 100 OFFSET")
            {
                return "RECOFF";
            }

            if (stock.ToLower().Contains("silk") || stock.Contains("Coated"))
                paper = "SIL";

            else if (stock.Contains("Uncoated") || stock.Contains("Edixion Offset") || stock.Contains("Edixion Laser"))
            {
                if (stock.Contains("Recycled"))
                    paper = "RECOFF";
                else
                    paper = "OFFSET";
            }
            else if (stock.ToLower().Contains("gloss"))
                paper = "GLOSS";
            return paper;
        }
    }
}
