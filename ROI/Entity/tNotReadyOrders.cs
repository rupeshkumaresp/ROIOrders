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
    
    public partial class tNotReadyOrders
    {
        public long ID { get; set; }
        public string ROIOrderID { get; set; }
        public string Status { get; set; }
        public Nullable<System.DateTime> CreatedAt { get; set; }
    }
}
