using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ROI
{
    /// <summary>
    /// GET ALL ROI ORDER AND PUSH TO THARSTERN
    /// </summary>
    public partial class FrmRoi : Form
    {
        private static readonly Logger Logger = new Logger();

        public FrmRoi()
        {
            InitializeComponent();
        }

        public static async Task RunAsync()
        {
            try
            {
                await RoiProcessingEngine.ProcessRoiOrders();
            }
            catch (Exception ex)
            {
                Logger.WriteLog("############ EXCEPETION: " + ex.InnerException);
            }
        }



    }
}
