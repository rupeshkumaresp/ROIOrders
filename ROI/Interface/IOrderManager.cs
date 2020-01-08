using System;

namespace ROI.Interface
{
    /// <summary>
    ///  INTERFACE ORDER MANAGER -- DOWNLOAD ORDERS FROM ROI, UPDATE THESE ORDERS IN THE DATABASE AND THEIR STATUS
    /// </summary>
    public interface IOrderManager
    {
        void DownloadRoiOrder(long orderId, double orderTotal, DateTime orderPlacesDate);
        void AddNonReadyOrders(string roiId, string status);
        bool CheckNotReadyOrders(string roiId);
        bool OrderAlreadyPushedToTharstern(long orderId);
        bool OrderFailedInProcessing(long orderId);
        void MarkOrderAllPartsPushedToTharstern(long orderId, int parts);
    }
}