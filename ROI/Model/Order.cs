using System;

namespace ROI.Model
{
    public class Order
    {
        public long OrderId { get; set; }
        public string ExternalId { get; set; }
        public string RoiOrderId { get; set; }
        public string PoNumber { get; set; }
        public double OrderTotal { get; set; }
        public string CurrencyUnit { get; set; }
        public DateTime OrderPlacedDate { get; set; }
        public int OrderPartsCount { get; set; }
        public string User { get; set; }
        public string TharsternJson { get; set; }
        public string AccountCode { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
    }
}
