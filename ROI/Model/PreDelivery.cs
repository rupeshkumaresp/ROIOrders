using System;
using System.Collections.Generic;

namespace ROI.Model
{
    public class Address
    {
        public List<string> AddressLines { get; set; }
        public string City { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
        public string Postcode { get; set; }
        public string Telephone { get; set; }
        public string Fax { get; set; }
    }

    public class SubJob
    {
        public int ID { get; set; }
        public int ProductNumber { get; set; }
        public int Quantity { get; set; }
    }

    public class PreDelivery
    {
        public string Ref1 { get; set; }
        public string Ref2 { get; set; }
        public DateTime RequiredDate { get; set; }
        public int AddressID { get; set; }
        public string AddressName { get; set; }
        public Address Address { get; set; }
        public string Contact { get; set; }
        public string ContactEmail { get; set; }
        public bool Collection { get; set; }
        public string JobNo { get; set; }
        public int JobID { get; set; }
        public int Quantity { get; set; }
        public List<SubJob> SubJobs { get; set; }
    }
}
