using ROI.Entity;
using ROI.Interface;

namespace ROI
{
    /// <summary>
    /// DATABASE LOGGER - EVENT, THARSTERN ERRORS/STATUS
    /// </summary>
    public class Logger : ILogger
    {
        private readonly ROIEntities _context = new ROIEntities();

        public void WriteLog(string message)
        {
            var log = new tSystemLog
            {
                Message = message,
                DateTime = System.DateTime.Now
            };

            _context.tSystemLog.Add(log);
            _context.SaveChanges();
        }
    }
}
