using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using nsTharsternAPI.Interfaces;

namespace nsTharsternAPI
{
    /// <summary>
    /// THARSTERN API ENGINE - POST/GET REQUEST THARSTERN SERVICE ENDPOINTS
    /// </summary>
    public class ApiEngine : IAuthentication, IEstimate, ICustomer, IProduct, ISalesOrder, IUtility
    {
        public string ApiBaseUrl = String.Empty;

        #region Authentication variables
        public string ApiToken = String.Empty;
        #endregion

        #region  Esimate variables
        public int EstimateProductId;
        public int ProductTypeId;
        public string EstRequestSample = String.Empty;
        public string DeliveryRequest = String.Empty;
        public string CustomerAddressRequest = String.Empty;
        public string PreDeliveryJson = String.Empty;
        public string PreDeliveryJsonCustomer = String.Empty;
        public string CreateJobFromEstimateRequest = String.Empty;
        public string CreateContactFromAccountRequest = String.Empty;
        
        public int EstimateId;
        public string EstimateError = "";
        public string EstimateNo = "";
        public string JobNo = "";
        public int DeliveryAddressId = 0;
        public int CustomerAddressId = 0;
        public string JobDeliveryNoteNo = string.Empty;
        public string CustomerDeliveryNoteNo = string.Empty;
        public int EstimateQuantity = 0;
        public string EstimateProductCode = null;
        #endregion

        #region Product variables
        public int ProductId;
        public string ProductCode = String.Empty;
        public int ProductQuantity;
        #endregion

        #region Customer variables
        public string CustomerCode = String.Empty;
        public int CustomerId;
        public string CustomerJson = string.Empty;
        #endregion

        #region Order variables
        public int OrderId;
        #endregion

        #region Utility

        public HttpRequestMessage CloneHTTPRequestMessages(HttpRequestMessage requestMessage, StreamContent clonedStreamContent)
        {
            HttpRequestMessage cloneRequestMessage = new HttpRequestMessage(requestMessage.Method, requestMessage.RequestUri);

            // Copy the request's content (via a MemoryStream) into the cloned object
            MemoryStream ms = new MemoryStream();
            if (requestMessage.Content != null)
            {
                cloneRequestMessage.Content = clonedStreamContent;

                // Copy the content headers
                if (requestMessage.Content.Headers != null)
                {
                    foreach (KeyValuePair<string, IEnumerable<string>> h in requestMessage.Content.Headers)
                    {
                        try
                        {
                            cloneRequestMessage.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }


            cloneRequestMessage.Version = requestMessage.Version;

            foreach (KeyValuePair<string, object> prop in requestMessage.Properties)
            {
                try
                {
                    cloneRequestMessage.Properties.Add(prop);
                }
                catch (Exception)
                {
                }
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in requestMessage.Headers)
            {
                try
                {
                    cloneRequestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                catch (Exception)
                {
                }
            }

            return cloneRequestMessage;
        }

        public async Task<HttpResponseMessage> CloneHTTPResponseMessages(HttpRequestMessage clonedRequestMessage, HttpResponseMessage responseMessage)
        {
            HttpResponseMessage cloneResponseMessage = new HttpResponseMessage((System.Net.HttpStatusCode)responseMessage.StatusCode);

            // Copy the response's content (via a MemoryStream) into the cloned object
            MemoryStream ms = new MemoryStream();
            if (responseMessage.Content != null)
            {
                //try json
                string json = await responseMessage.Content.ReadAsStringAsync();
                if (!String.IsNullOrEmpty(json))
                {
                    try
                    {
                        json = JValue.Parse(json).ToString(Formatting.Indented);
                    }
                    catch (Exception)
                    {
                        json = null;
                    }
                }

                if (String.IsNullOrEmpty(json))
                {
                    await responseMessage.Content.CopyToAsync(ms).ConfigureAwait(false);
                    ms.Position = 0;
                    cloneResponseMessage.Content = new StreamContent(ms);
                }
                else
                {
                    cloneResponseMessage.Content = new StringContent(json);
                }

                // Copy the content headers
                if (responseMessage.Content.Headers != null)
                {
                    foreach (KeyValuePair<string, IEnumerable<string>> h in responseMessage.Content.Headers)
                    {
                        try
                        {
                            cloneResponseMessage.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

            cloneResponseMessage.Version = responseMessage.Version;
            cloneResponseMessage.ReasonPhrase = responseMessage.ReasonPhrase;
            responseMessage.RequestMessage = clonedRequestMessage;

            foreach (KeyValuePair<string, IEnumerable<string>> header in responseMessage.Headers)
            {
                try
                {
                    cloneResponseMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                catch (Exception)
                {
                }
            }

            return cloneResponseMessage;
        }

        public string ToQueryString(NameValueCollection nvc)
        {
            string[] array = (from key in nvc.AllKeys
                              from value in nvc.GetValues(key)
                              select string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(value))).ToArray();

            return "?" + string.Join("&", array);
        }

        public string GetAPIMethodUrl(string apiMethod,
            NameValueCollection nvc)
        {
            string apiMethodUrl = ApiBaseUrl;

            if (!apiMethodUrl.EndsWith("/"))
            {
                apiMethodUrl += "/";
            }

            apiMethodUrl += "api/";

            apiMethodUrl += apiMethod;

            if (nvc != null && nvc.Count > 0)
            {
                apiMethodUrl += ToQueryString(nvc);
            }

            return apiMethodUrl;
        }

        public async Task<dynamic> GetObjectAsync(string apiMethod, NameValueCollection nvc)
        {
            dynamic d = null;

            System.Net.Http.HttpClient c = new System.Net.Http.HttpClient();

            string response = String.Empty;

            c.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            if (!String.IsNullOrEmpty(ApiToken))
            {
                c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.Default.GetBytes(ApiToken + ":")));
            }

            string apiUrl = GetAPIMethodUrl(
                apiMethod,
                nvc
            );

            using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, apiUrl))
            {
                HttpRequestMessage clonedRequestMessage = null;

                using (HttpResponseMessage responseMessage = await c.SendAsync(requestMessage))
                {
                    if (responseMessage != null)
                    {
                        response = responseMessage.Content.ReadAsStringAsync().Result;
                    }


                }
            }

            if (!String.IsNullOrEmpty(response))
            {
                try
                {
                    d = JsonConvert.DeserializeObject(response);
                }
                catch (Exception)
                {
                }
            }

            return d;
        }

        public async Task<dynamic> GetObjectFromPostAsync(string apiMethod, string toPost)
        {
            dynamic d = null;

            System.Net.Http.HttpClient c = new System.Net.Http.HttpClient();

            string response = String.Empty;
            string apiUrl = String.Empty;

            apiUrl = GetAPIMethodUrl(
                apiMethod,
                null
            );

            c.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            if (!String.IsNullOrEmpty(ApiToken))
            {
                c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.Default.GetBytes(ApiToken + ":")));
            }

            using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl))
            {
                requestMessage.Content = new StringContent(toPost, Encoding.UTF8, "application/json");

                HttpRequestMessage clonedRequestMessage = null;
                StreamContent sc = null;


                using (HttpResponseMessage responseMessage = await c.SendAsync(requestMessage))
                {
                    if (responseMessage != null)
                    {
                        response = responseMessage.Content.ReadAsStringAsync().Result;
                    }


                }
            }

            if (!String.IsNullOrEmpty(response))
            {
                try
                {
                    d = JsonConvert.DeserializeObject(response);
                }
                catch (Exception)
                {
                }
            }

            return d;
        }

        #endregion

        #region Authentication

        public async Task<string> SetApiTokenAsyncForWeb(string email, string password)
        {
            ApiToken = null;

            dynamic d = await GetObjectAsync(
                "Authentication/GenerateAPIToken",
                new NameValueCollection {
                    { "email", email },
                    { "password", password },
                    { "applicationId", "api" }
                }
            );

            if (d != null)
            {
                if (String.Compare(d.Status.Success.ToString(), "true", true) == 0)
                {
                    Guid g = Guid.Empty;

                    if (Guid.TryParse(d.Details.Token.ToString(), out g))
                    {
                        if (g != Guid.Empty)
                        {
                            ApiToken = g.ToString();
                        }
                    }
                }
            }

            return ApiToken;
        }

        public async Task SetApiTokenAsync(string email, string password)
        {
            ApiToken = null;

            dynamic d = await GetObjectAsync(
                "Authentication/GenerateAPIToken",
                new NameValueCollection {
                    { "email", email },
                    { "password", password },
                    { "applicationId", "api" }
                }
            );

            if (d != null)
            {
                if (String.Compare(d.Status.Success.ToString(), "true", true) == 0)
                {
                    Guid g = Guid.Empty;

                    if (Guid.TryParse(d.Details.Token.ToString(), out g))
                    {
                        if (g != Guid.Empty)
                        {
                            ApiToken = g.ToString();
                        }
                    }
                }
            }

        }
        #endregion

        #region Estimate

        public async Task RetrieveEstRequestSampleAsync(string productCode, int quantity)
        {
            EstRequestSample = null;
            ProductTypeId = 0;
            EstimateProductId = 0;
            EstimateQuantity = 0;
            EstimateProductCode = null;

            dynamic d = await GetObjectAsync(
                "products",
                new NameValueCollection {
                    { "productCode", productCode }
                }
            );

            if (d != null)
            {
                if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0 && d.Details.Items != null && d.Details.Items.Count > 0)
                {
                    int.TryParse(d.Details.Items[0].ProductTypes_ID.ToString(), out ProductTypeId);

                    if (ProductTypeId > 0)
                    {
                        int.TryParse(d.Details.Items[0].ID.ToString(), out EstimateProductId);
                    }
                }

                d = null;
            }

            if (ProductTypeId > 0)
            {
                d = await GetObjectAsync(
                    String.Format("EstRequest/{0}/Example", ProductTypeId),
                    null
                );

                if (d != null)
                {
                    if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0)
                    {
                        if (d.Details != null)
                        {
                            d.Details.Customer.Code = CustomerCode;
                            d.Details.DeliveryCustomer.Code = CustomerCode;
                            d.Details.Contact = null;
                            d.Details.DeliveryContact = null;

                            EstRequestSample = d.Details.ToString();
                            EstimateQuantity = quantity;
                            EstimateProductCode = productCode;
                        }
                    }
                }
            }

            if (String.IsNullOrEmpty(EstRequestSample))
            {
                EstRequestSample = null;
                ProductTypeId = 0;
                EstimateProductId = 0;
                EstimateQuantity = 0;
            }
        }

        public async Task CreatePreDeliveryAsync()
        {
            if (!String.IsNullOrEmpty(PreDeliveryJson))
            {
                dynamic d = await GetObjectFromPostAsync(
                    "/job/createpredelivery",
                    PreDeliveryJson
                );

                if (d != null)
                {
                    if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0)
                    {
                        try
                        {
                            if (d.Details != null)
                            {
                                string responseString = d.ToString();

                                if (responseString != null)
                                {
                                    int pos1 = responseString.IndexOf("DeliveryNoteID") + "DeliveryNoteID".Length;

                                    int pos2 = responseString.IndexOf("Result");

                                    var DelNoteId = responseString.Substring(pos1, pos2 - pos1);


                                    DelNoteId = DelNoteId.Trim();
                                    DelNoteId = DelNoteId.Replace(Environment.NewLine, "");
                                    DelNoteId = DelNoteId.Replace("\\r\\n", "");
                                    DelNoteId = DelNoteId.Replace("\\", string.Empty);
                                    DelNoteId = DelNoteId.Replace(":", string.Empty);
                                    DelNoteId = DelNoteId.Replace(@"\", "");
                                    DelNoteId = DelNoteId.Replace("\"", "");
                                    DelNoteId = DelNoteId.Replace(",", "");
                                    DelNoteId = DelNoteId.Replace("\"", "\\\"");
                                    DelNoteId = DelNoteId.Replace(@"""", @"\""");

                                    DelNoteId = DelNoteId.Trim();
                                    JobDeliveryNoteNo = DelNoteId;
                                }

                            }
                        }
                        catch { }
                    }
                }
            }

        }

        public async Task CreatePreDeliveryCustomerAsync()
        {
            if (!String.IsNullOrEmpty(PreDeliveryJsonCustomer))
            {
                dynamic d = await GetObjectFromPostAsync(
                    "/job/createpredelivery",
                    PreDeliveryJsonCustomer
                );

                if (d != null)
                {
                    if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0)
                    {
                        try
                        {
                            if (d.Details != null)
                            {
                                string responseString = d.ToString();

                                if (responseString != null)
                                {
                                    int Pos1 = responseString.IndexOf("DeliveryNoteID") + "DeliveryNoteID".Length;

                                    int Pos2 = responseString.IndexOf("Result");

                                    var DelNoteId = responseString.Substring(Pos1, Pos2 - Pos1);


                                    DelNoteId = DelNoteId.Trim();
                                    DelNoteId = DelNoteId.Replace(Environment.NewLine, "");
                                    DelNoteId = DelNoteId.Replace("\\r\\n", "");
                                    DelNoteId = DelNoteId.Replace("\\", string.Empty);
                                    DelNoteId = DelNoteId.Replace(":", string.Empty);
                                    DelNoteId = DelNoteId.Replace(@"\", "");
                                    DelNoteId = DelNoteId.Replace("\"", "");
                                    DelNoteId = DelNoteId.Replace(",", "");
                                    DelNoteId = DelNoteId.Replace("\"", "\\\"");
                                    DelNoteId = DelNoteId.Replace(@"""", @"\""");


                                    DelNoteId = DelNoteId.Trim();
                                    CustomerDeliveryNoteNo = DelNoteId;
                                }


                            }
                        }
                        catch { }
                    }
                }
            }

        }

        public async Task CreateEstimateAsync()
        {
            EstimateId = 0;

            if (!String.IsNullOrEmpty(EstRequestSample))
            {
                dynamic d = await GetObjectFromPostAsync(
                    "estrequest",
                    EstRequestSample
                );

                if (d != null)
                {
                    if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0)
                    {
                        try
                        {
                            if (d.Details != null)
                            {
                                string responseString = d.ToString();

                                var indexOfEstimate = responseString.IndexOf("Estimate");
                                var indexOfEstimateRef = responseString.IndexOf("EstimateRef");


                                var temp = responseString.Substring(indexOfEstimateRef,
                                    responseString.Length - indexOfEstimateRef);

                                var estiamteRef = temp.Substring(0, temp.IndexOf(","));
                                estiamteRef = estiamteRef.Replace("EstimateRef", "");
                                estiamteRef = estiamteRef.Replace(":", "");
                                estiamteRef = estiamteRef.Trim();

                                estiamteRef = estiamteRef.Replace(Environment.NewLine, "");
                                estiamteRef = estiamteRef.Replace("\\r\\n", "");
                                estiamteRef = estiamteRef.Replace("\\", string.Empty);
                                estiamteRef = estiamteRef.Replace(@"\", "");
                                estiamteRef = estiamteRef.Replace("\"", "");
                                estiamteRef = estiamteRef.Replace(",", "");

                                EstimateNo = estiamteRef;
                                var id = responseString.Substring(indexOfEstimate + 12, indexOfEstimateRef - indexOfEstimate - 12);

                                id = id.Trim();
                                id = id.Replace(Environment.NewLine, "");
                                id = id.Replace("\\r\\n", "");
                                id = id.Replace("\\", string.Empty);
                                id = id.Replace("ID", string.Empty);
                                id = id.Replace(":", string.Empty);
                                id = id.Replace(@"\", "");
                                id = id.Replace("\"", "");
                                id = id.Replace(",", "");

                                id = id.Trim();

                                int.TryParse(id, out EstimateId);
                                EstimateError = string.Empty;
                            }
                        }
                        catch (Exception e)
                        {
                            EstimateId = 0;
                            var errorMessage = d.ToString();
                            var indexOfProblem = errorMessage.IndexOf("Problems");

                            var error = errorMessage.Substring(indexOfProblem + 9,
                                errorMessage.Length - indexOfProblem - 9);

                            error = error.Replace("[", "");
                            error = error.Replace("]", "");
                            error = error.Replace("}", "");
                            error = error.Replace(":", "");
                            error = error.Replace(Environment.NewLine, "");
                            error = error.Replace("\\r\\n", "");
                            error = error.Trim();

                            EstimateError = error;
                        }

                    }
                    else
                    {

                        var errorMessage = d.ToString();
                        var indexOfProblem = errorMessage.IndexOf("Problems");

                        var error = errorMessage.Substring(indexOfProblem + 9,
                            errorMessage.Length - indexOfProblem - 9);

                        error = error.Replace("[", "");
                        error = error.Replace("]", "");
                        error = error.Replace("}", "");
                        error = error.Replace(":", "");
                        error = error.Replace(Environment.NewLine, "");
                        error = error.Replace("\\r\\n", "");
                        error = error.Trim();

                        EstimateError = error;
                    }
                }
            }
        }

        public async Task CreateDeliveryAsync()
        {

            if (!String.IsNullOrEmpty(DeliveryRequest))
            {
                dynamic d = await GetObjectFromPostAsync(
                    "address/create",
                    DeliveryRequest
                );

                if (d != null)
                {
                    if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0)
                    {
                        try
                        {
                            if (d.Details != null)
                            {
                                string responseString = d.ToString();

                                var indexOfItems = responseString.IndexOf("Items");
                                var indexOfCode = responseString.IndexOf("Code");

                                var id = responseString.Substring(indexOfItems + 7, indexOfCode - indexOfItems - 7);

                                id = id.Trim();
                                id = id.Replace(Environment.NewLine, "");
                                id = id.Replace("\\r\\n", "");
                                id = id.Replace("\\", string.Empty);
                                id = id.Replace("ID", string.Empty);
                                id = id.Replace(":", string.Empty);
                                id = id.Replace("[", string.Empty);
                                id = id.Replace("{", string.Empty);
                                id = id.Replace(@"\", "");
                                id = id.Replace("\"", "");
                                id = id.Replace(",", "");

                                id = id.Trim();

                                int.TryParse(id, out DeliveryAddressId);
                            }
                        }
                        catch (Exception e)
                        {
                            DeliveryAddressId = 0;
                        }

                    }
                }
            }
        }

        public async Task CreateCustomerAddressAsync()
        {

            if (!String.IsNullOrEmpty(CustomerAddressRequest))
            {
                dynamic d = await GetObjectFromPostAsync(
                    "address/create",
                    CustomerAddressRequest
                );

                if (d != null)
                {
                    if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0)
                    {
                        try
                        {
                            if (d.Details != null)
                            {
                                string responseString = d.ToString();

                                var indexOfItems = responseString.IndexOf("Items");
                                var indexOfCode = responseString.IndexOf("Code");

                                var id = responseString.Substring(indexOfItems + 7, indexOfCode - indexOfItems - 7);

                                id = id.Trim();
                                id = id.Replace(Environment.NewLine, "");
                                id = id.Replace("\\r\\n", "");
                                id = id.Replace("\\", string.Empty);
                                id = id.Replace("ID", string.Empty);
                                id = id.Replace(":", string.Empty);
                                id = id.Replace("[", string.Empty);
                                id = id.Replace("{", string.Empty);
                                id = id.Replace(@"\", "");
                                id = id.Replace("\"", "");
                                id = id.Replace(",", "");

                                id = id.Trim();

                                int.TryParse(id, out CustomerAddressId);
                            }
                        }
                        catch (Exception e)
                        {
                            CustomerAddressId = 0;
                        }

                    }
                }
            }
        }

        public async Task CreateJobFromEstimateAsync()
        {

            if (!String.IsNullOrEmpty(CreateJobFromEstimateRequest))
            {
                dynamic d = await GetObjectFromPostAsync(
                    "job/createfromestimate",
                    CreateJobFromEstimateRequest
                );

                if (d != null)
                {
                    if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0)
                    {
                        try
                        {
                            if (d.Details != null)
                            {
                                string response = d.ToString();

                                var temp = response.Substring(response.IndexOf("JobNo"),
                                    response.Length - response.IndexOf("JobNo"));

                                var jobNo = temp.Substring(0, temp.IndexOf(","));
                                jobNo = jobNo.Replace("JobNo", "");
                                jobNo = jobNo.Replace(":", "");
                                jobNo = jobNo.Trim();

                                jobNo = jobNo.Replace(Environment.NewLine, "");
                                jobNo = jobNo.Replace("\\r\\n", "");
                                jobNo = jobNo.Replace("\\", string.Empty);
                                jobNo = jobNo.Replace(@"\", "");
                                jobNo = jobNo.Replace("\"", "");
                                jobNo = jobNo.Replace(",", "");
                                jobNo = jobNo.Trim();

                                JobNo = jobNo;



                            }
                        }
                        catch (Exception e)
                        {
                        }

                    }
                }
            }
        }

        public async Task<bool> SearchContactForAccount(string customerCode, string Name, string email)
        {

            dynamic d = await GetObjectAsync(
              "contacts",
              new NameValueCollection {
                    { "customerCode", customerCode }
                }
          );



            if (d != null)
            {
                if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0)
                {
                    try
                    {
                        if (d.Details != null)
                        {
                            string response = d.ToString();

                            if (response.Contains(Name) && response.Contains(email))
                                return true;


                            return false;


                        }
                    }
                    catch (Exception e)
                    {
                    }

                }
            }

            return false;

        }

        public async Task CreateContactForAccount()
        {

            if (!String.IsNullOrEmpty(CreateContactFromAccountRequest))
            {
                dynamic d = await GetObjectFromPostAsync(
                    "contacts",
                    CreateContactFromAccountRequest
                );

                if (d != null)
                {
                    if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0)
                    {
                        try
                        {
                            if (d.Details != null)
                            {
                                //string response = d.ToString();

                                //var temp = response.Substring(response.IndexOf("JobNo"),
                                //    response.Length - response.IndexOf("JobNo"));

                                //var jobNo = temp.Substring(0, temp.IndexOf(","));
                                //jobNo = jobNo.Replace("JobNo", "");
                                //jobNo = jobNo.Replace(":", "");
                                //jobNo = jobNo.Trim();

                                //jobNo = jobNo.Replace(Environment.NewLine, "");
                                //jobNo = jobNo.Replace("\\r\\n", "");
                                //jobNo = jobNo.Replace("\\", string.Empty);
                                //jobNo = jobNo.Replace(@"\", "");
                                //jobNo = jobNo.Replace("\"", "");
                                //jobNo = jobNo.Replace(",", "");
                                //jobNo = jobNo.Trim();

                                //JobNo = jobNo;



                            }
                        }
                        catch (Exception e)
                        {
                        }

                    }
                }
            }
        }


        #endregion

        #region Customer

        public async Task<string> RetrieveCustomerJsonAsyncForWeb(string customerCode)
        {
            CustomerCode = String.Empty;
            CustomerId = 0;

            dynamic d = await GetObjectAsync(
                "customers",
                new NameValueCollection {
                    { "customerCode", customerCode }
                }
            );

            if (d != null)
            {
                CustomerJson = d.ToString();
                if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0 && d.Details.Items != null && d.Details.Items.Count > 0)
                {
                    CustomerCode = d.Details.Items[0].Code.ToString();
                    CustomerId = d.Details.Items[0].ID ?? 0;
                }
                return d.ToString();
            }
            else
                return string.Empty;
        }

        public async Task RetrieveCustomerAsync(string customerCode)
        {
            CustomerCode = String.Empty;
            CustomerId = 0;

            dynamic d = await GetObjectAsync(
                "customers",
                new NameValueCollection {
                    { "customerCode", customerCode }
                }
            );

            CustomerJson = d.ToString();
            if (d != null)
            {
                if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0 && d.Details.Items != null && d.Details.Items.Count > 0)
                {
                    CustomerCode = d.Details.Items[0].Code.ToString();
                    CustomerId = d.Details.Items[0].ID ?? 0;
                }

                d = null;
            }
        }
        #endregion

        #region Product
        public async Task RetrieveProductAsync(string productCode, int quantity)
        {
            ProductId = 0;
            ProductQuantity = 0;
            ProductCode = String.Empty;

            dynamic d = await GetObjectAsync(
                "products",
                new NameValueCollection {
                    { "productCode", productCode }
                }
            );

            if (d != null)
            {
                if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0 && d.Details.Items != null && d.Details.Items.Count > 0)
                {
                    int.TryParse(d.Details.Items[0].ID.ToString(), out ProductId);

                    if (ProductId > 0)
                    {
                        ProductQuantity = quantity;
                        ProductCode = productCode;
                    }
                }

                d = null;
            }
        }
        #endregion

        #region Sales Order
        public async Task CreateSalesOrderAsync()
        {
            OrderId = 0;

            //you would likely use objects here that are serialized to json (or xml).
            string json = String.Empty;

            if (ProductId > 0 && EstimateId > 0)
            {
                json = String.Format("{{\"Orders\": [{{\"UniqueSubmitOrderID\": \"{0}\",\"InvoiceCustomer\": {{\"Code\": \"{1}\"}},\"RequiredDate\": \"{2}\",\"OrderDate\": \"{3}\",\"Ref1\": \"My Order Ref 1\",\"Ref2\": \"My Order Ref 2\",\"Items\": [{{\"StockItemID\": {4},\"Quantity\": {5},\"Ref1\": \"My Line Ref 1\",\"Ref2\": \"My Line Ref 2\"}},{{\"StockItemID\": {6},\"EstimateID\": {7},\"Quantity\": {8},\"Ref1\": \"My Est Line Ref 1\",\"Ref2\": \"My Est Line Ref 2\"}}]}}]}}",
                    Guid.NewGuid().ToString(),
                    CustomerCode,
                    DateTime.Now.AddDays(14).ToUniversalTime().ToString("o"),
                    DateTime.Now.ToUniversalTime().ToString("o"),
                    ProductId,
                    ProductQuantity,
                    EstimateProductId,
                    EstimateId,
                    EstimateQuantity
                );
            }
            else if (ProductId > 0)
            {
                json = String.Format("{{\"Orders\": [{{\"UniqueSubmitOrderID\": \"{0}\",\"InvoiceCustomer\": {{\"Code\": \"{1}\"}},\"RequiredDate\": \"{2}\",\"OrderDate\": \"{3}\",\"Ref1\": \"My Order Ref 1\",\"Ref2\": \"My Order Ref 2\",\"Items\": [{{\"StockItemID\": {4},\"Quantity\": {5},\"Ref1\": \"My Line Ref 1\",\"Ref2\": \"My Line Ref 2\"}}]}}]}}",
                    Guid.NewGuid().ToString(),
                    CustomerCode,
                    DateTime.Now.AddDays(14).ToUniversalTime().ToString("o"),
                    DateTime.Now.ToUniversalTime().ToString("o"),
                    ProductId,
                    ProductQuantity
                );
            }
            //
            else if (EstimateId > 0)
            {
                json = String.Format("{{\"Orders\": [{{\"UniqueSubmitOrderID\": \"{0}\",\"InvoiceCustomer\": {{\"Code\": \"{1}\"}},\"RequiredDate\": \"{2}\",\"OrderDate\": \"{3}\",\"Ref1\": \"My Order Ref 1\",\"Ref2\": \"My Order Ref 2\",\"Items\": [{{\"StockItemID\": {4},\"Quantity\": 100,\"Ref1\": \"My Line Ref 1\",\"Ref2\": \"My Line Ref 2\"}},{{\"StockItemID\": {4},\"EstimateID\": {5},\"Quantity\": {6},\"Ref1\": \"My Est Line Ref 1\",\"Ref2\": \"My Est Line Ref 2\"}}]}}]}}",
                    Guid.NewGuid().ToString(),
                    CustomerCode,
                    DateTime.Now.AddDays(14).ToUniversalTime().ToString("o"),
                    DateTime.Now.ToUniversalTime().ToString("o"),
                    EstimateProductId,
                    EstimateId,
                    EstimateQuantity
                );
            }

            dynamic d = await GetObjectFromPostAsync(
                "orders/submit",
                json
            );

            if (d != null)
            {
                if (String.Compare(d.Status.Success.Value.ToString(), "true", true) == 0 && d.Details.Orders != null && d.Details.Orders.Count > 0)
                {
                    int.TryParse(d.Details.Orders[0].ID.ToString(), out OrderId);
                }

                d = null;
            }
        }
        #endregion
    }
}
