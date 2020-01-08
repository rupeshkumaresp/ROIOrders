using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ROI.Model
{
    [Serializable]
    public class TharEstimateRequest
    {
        //public int ID { get; set; }
        public string Title { get; set; }
        public int ProductTypeId { get; set; }
        public int Quantity { get; set; }
        public string RequiredDateTime { get; set; }
        public string JobType { get; set; }
        public string Ref1 { get; set; }
        public string Ref2 { get; set; }
        public string Ref3 { get; set; }
        public string Ref4 { get; set; }
        public string Ref5 { get; set; }
        public string Ref6 { get; set; }
        public string Ref7 { get; set; }
        public string Ref8 { get; set; }
        public string Ref9 { get; set; }
        public string Ref10 { get; set; }
        public decimal TotalPrice { get; set; }

        public tharEstFinishedSize FinishedSize { get; set; }
        public TharEstCustomer Customer { get; set; }
        public TharEstCustomer DeliveryCustomer { get; set; }
        public List<TharEstParts> Parts { get; set; }
        public List<tharProcesses> Processes { get; set; }
        public tharOrientation Orientation { get; set; }

        [Serializable]
        public class tharEstFinishedSize
        {
            public string Code { get; set; }
            public int? Width { get; set; }
            public int? Depth { get; set; }
        }

        [Serializable]
        public class TharEstCustomer
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string Contact { get; set; }
            public string ContactEmail { get; set; }
            public string Ref1 { get; set; }
            public string Ref2 { get; set; }
            public string Ref3 { get; set; }
            public string TimeStamp { get; set; }
            public tharAddress Address { get; set; }
        }

        [Serializable]
        public class Contact
        {
            public string Name { get; set; }
            public string CustomerCode { get; set; }            
            public string Email { get; set; }
            public string Telephone { get; set; }
            public string Mobile { get; set; }            
        }


        [Serializable]
        public class tharAddress
        {
            public string[] AddressLines { get; set; }
            public string City { get; set; }
            public string Region { get; set; }
            public string Country { get; set; }
            public string Postcode { get; set; }
            public string Telephone { get; set; }
            public string Fax { get; set; }
        }
        [Serializable]
        public class TharEstParts
        {
            public string Name { get; set; }
            public int Pages { get; set; }
            public string PaperCode { get; set; }
            public int? FoldingHeaderId { get; set; }
            public int? ImpositionTemplateId { get; set; }
            public int? SpineThickness { get; set; }
            public int? SpineToLeftEdge { get; set; }
            public int? SpineToRightEdge { get; set; }
            public bool? SameColours { get; set; }
            public bool? SameImage { get; set; }
            public bool? Bleed { get; set; }
            public bool? IsFlatSize { get; set; }
            public bool? SameWidths { get; set; }
            public int? GSM { get; set; }
            public string FrontProcess { get; set; }
            public string FrontSpots { get; set; }
            public string FrontMetallics { get; set; }
            public string BackProcess { get; set; }
            public string BackSpots { get; set; }
            public string BackMetallics { get; set; }
            public string Coatings { get; set; }
            public string Seal1Side { get; set; }
            public string Seal1Type { get; set; }
            public string Seal2Side { get; set; }
            public string Seal2Type { get; set; }
            public int ProductTypePartId { get; set; }
            public string SpecificPress { get; set; }
            public int? Quantity { get; set; }
            public int? NumberCuts { get; set; }
            public bool? IsForceSheetWork { get; set; }
            public int? FlatWidth { get; set; }

            public tharEstFinishedSize FinishedSize { get; set; }
            public List<tharProcesses> Processes { get; set; }
            public tharEstBindingSide BindingSide { get; set; }

        }

        [Serializable]
        public class tharEstBindingSide
        {
            public int Value { get; set; }
            public string EnumType { get; set; }

        }

        [Serializable]
        public class tharOrientation
        {
            public int Value { get; set; }
            public string EnumType { get; set; }
        }

        //public class CoverType
        //{
        //    public int Value { get; set; }
        //    public string EnumType { get; set; }
        //}

        [Serializable]
        public class tharProcesses
        {
            public int? ProcessTypeID { get; set; }
            public int? CostCentreID { get; set; }
            public int? OutworkID { get; set; }
            public string MaterialCode { get; set; }
            public double? Value { get; set; }
        }


        public tharEstBindingSide BindingSide { get; set; }
    }

    [Serializable]
    public class TharJobFromEstimate
    {
        public string EstimateID { get; set; }
        public string Ref1 { get; set; }
        public string Ref2 { get; set; }
        public bool SetAsProductionReady { get; set; }
        public bool SubmitToWorkflow { get; set; }
    }

    [Serializable]
    public class TharDeliveryAddress
    {
        public string CustomerCode { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        [JsonProperty(PropertyName = "Address")]
        public TharDeliveryAddressDetails AddressDetails { get; set; }
        [Serializable]
        public class TharDeliveryAddressDetails
        {
            public string[] AddressLines { get; set; }
            public string City { get; set; }
            public string Region { get; set; }
            public string Country { get; set; }
            public string Postcode { get; set; }
            public string Telephone { get; set; }
            public string Fax { get; set; }
        }
        public string Contact { get; set; }
        public string ContactEmail { get; set; }
    }
}
