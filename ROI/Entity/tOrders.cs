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
    
    public partial class tOrders
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public tOrders()
        {
            this.tOrderTharsternPushDetails = new HashSet<tOrderTharsternPushDetails>();
        }
    
        public long OrderID { get; set; }
        public long ROIOrderId { get; set; }
        public Nullable<System.DateTime> CreatedAt { get; set; }
        public Nullable<decimal> OrderTotal { get; set; }
        public Nullable<System.DateTime> OrderPlacedDate { get; set; }
        public Nullable<bool> PushedToTharstern { get; set; }
        public Nullable<bool> FailedInProcessing { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<tOrderTharsternPushDetails> tOrderTharsternPushDetails { get; set; }
    }
}
