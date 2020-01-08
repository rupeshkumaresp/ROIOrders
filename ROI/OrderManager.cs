using ROI.Entity;
using System;
using System.Linq;
using ROI.Interface;

namespace ROI
{

    /// <summary>
    /// ORDER MANAGER -- DOWNLOAD ORDERS FROM ROI, UPDATE THESE ORDERS IN THE DATABASE AND THEIR STATUS
    /// </summary>
    public class OrderManager : IOrderManager
    {
        readonly ROIEntities _context = new ROIEntities();

        public void DownloadRoiOrder(long orderId, double orderTotal, DateTime orderPlacesDate)
        {

            var order = _context.tOrders.FirstOrDefault(o => o.ROIOrderId == orderId);

            if (order == null)
            {
                order = new tOrders
                {
                    ROIOrderId = orderId,
                    CreatedAt = DateTime.Now,
                    OrderTotal = Convert.ToDecimal(orderTotal),
                    OrderPlacedDate = orderPlacesDate
                };
                _context.tOrders.Add(order);
                _context.SaveChanges();
            }

        }

        public void AddNonReadyOrders(string roiId, string status)
        {
            tNotReadyOrders order = new tNotReadyOrders { ROIOrderID = roiId, Status = status, CreatedAt = DateTime.Now };
            _context.tNotReadyOrders.Add(order);

            var id = Convert.ToInt64(roiId);

            var orderInDb = _context.tOrders.FirstOrDefault(o => o.ROIOrderId == id);

            if (orderInDb != null)
                orderInDb.FailedInProcessing = true;

            _context.SaveChanges();

        }

        public bool CheckNotReadyOrders(string roiId)
        {
            tNotReadyOrders order = _context.tNotReadyOrders.FirstOrDefault(o => o.ROIOrderID == roiId);

            if (order != null)
                return true;

            return false;

        }

        public bool OrderAlreadyPushedToTharstern(long orderId)
        {
            var order = _context.tOrders.FirstOrDefault(o => o.ROIOrderId == orderId);

            if (order == null)
                return false;

            var orderTharsternPushDetails = _context.tOrderTharsternPushDetails.Where(o => o.OrderID == order.OrderID).ToList();

            if (orderTharsternPushDetails.Count == 0)
                return false;

            var allPushed = true;

            foreach (var tharsternPushDetail in orderTharsternPushDetails)
            {

                if (!Convert.ToBoolean(tharsternPushDetail.PushedToTharstern))
                {
                    allPushed = false;
                    break;
                }
            }

            return allPushed;
        }

        public bool OrderFailedInProcessing(long orderId)
        {
            var order = _context.tOrders.FirstOrDefault(o => o.ROIOrderId == orderId);

            if (order == null)
                return false;

            return Convert.ToBoolean(order.FailedInProcessing);
        }

        public void MarkOrderAllPartsPushedToTharstern(long orderId, int parts)
        {
            var order = _context.tOrders.FirstOrDefault(o => o.ROIOrderId == orderId);

            if (order == null)
                return;

            var orderTharsternPushDetails = _context.tOrderTharsternPushDetails.Where(o => o.OrderID == order.OrderID).ToList();

            if (orderTharsternPushDetails.Count == 0)
                return;


            int countOfPushedParts = 0;
            foreach (var tharsternPushDetail in orderTharsternPushDetails)
            {

                if (Convert.ToBoolean(tharsternPushDetail.PushedToTharstern))
                {
                    countOfPushedParts++;
                }
            }

            if (countOfPushedParts == parts)
            {
                order.PushedToTharstern = true;
                _context.SaveChanges();
            }

        }

    }
}
