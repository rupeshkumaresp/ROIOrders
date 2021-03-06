//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ROI.Entity
{
    using System;
    using System.Collections.Generic;
    
    public partial class DeliveryNote
    {
        public int ID { get; set; }
        public string Instructions { get; set; }
        public string AdditionalNotes { get; set; }
        public byte Status { get; set; }
        public bool IsPreDelivery { get; set; }
        public string DelNoteNumber { get; set; }
        public Nullable<System.DateTime> Datedb { get; set; }
        public string OrderNo { get; set; }
        public string GRNNo { get; set; }
        public string DeliveryCode { get; set; }
        public string DeliveryName { get; set; }
        public string DeliveryAddress { get; set; }
        public string DeliveryTown { get; set; }
        public string DeliveryCounty { get; set; }
        public string DeliveryPostCode { get; set; }
        public string DeliveryCountry { get; set; }
        public string DeliveryContact { get; set; }
        public string DeliveryTel { get; set; }
        public string DeliveryFax { get; set; }
        public string FromCode { get; set; }
        public string FromName { get; set; }
        public string FromAddress { get; set; }
        public string FromTown { get; set; }
        public string FromCounty { get; set; }
        public string FromPostCode { get; set; }
        public string FromCountry { get; set; }
        public string FromContact { get; set; }
        public string FromTel { get; set; }
        public string FromFax { get; set; }
        public byte DeliveryType { get; set; }
        public string CustomerRef { get; set; }
        public string CustomerRef2 { get; set; }
        public string Courier { get; set; }
        public Nullable<System.DateTime> DelDateTime { get; set; }
        public string ConsignNote { get; set; }
        public string Contact { get; set; }
        public string DelConsignType { get; set; }
        public string SignedFor { get; set; }
        public Nullable<System.DateTime> ProofDateTime { get; set; }
        public Nullable<System.DateTime> ReqDate { get; set; }
        public string TakenBy { get; set; }
        public bool IsStockUpdated { get; set; }
        public Nullable<System.DateTime> CompletedDate { get; set; }
        public byte DelAddressType { get; set; }
        public byte FromAddressType { get; set; }
        public string DeliveryCountryOfOrigin { get; set; }
        public string FromCountryOfOrigin { get; set; }
        public int CourierServiceID { get; set; }
        public double CourierPrice { get; set; }
        public short Boxes { get; set; }
        public byte Pallets { get; set; }
        public double Weight { get; set; }
        public double TotalWeight { get; set; }
        public int CompanyID { get; set; }
        public bool CollectionOnly { get; set; }
        public Nullable<int> SecurityGroupID { get; set; }
        public string Email { get; set; }
        public string CourierStatus { get; set; }
        public byte CourierAccountType { get; set; }
        public int DespatchDetailsID { get; set; }
        public Nullable<System.DateTime> DateModified { get; set; }
        public Nullable<int> ModifiedByUserID { get; set; }
        public byte[] dbTimeStamp { get; set; }
        public string Phone { get; set; }
        public bool AutoCreatedFromPick { get; set; }
    }
}
