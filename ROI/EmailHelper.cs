using System;
using System.Net.Mail;

namespace ROI
{
    /// <summary>
    /// VARIOUS EMAIL TEMPLATES AND SEND EMAIL FUNCTIONALITY
    /// </summary>
    public class EmailHelper
    {
        private static readonly Logger Logger = new Logger();

        #region email Templates


        public static string AutoOrderPushNotificationResults =

      @"<p>
    	Hi All,</p>
        <p>
	        Here is the summary of Tharstern processing of ROI orders, please review the <b> ESTIMATES & JOBS </b> created in Tharstern. </p>
        <p>
	        [ORDERSTATUS]</p>
       	       
        <p>
	        Kind Regards,</p>
        <p>
	        ESP</p>
        <p>
	        <span style='color:#696969;'><em><span style='font-size: 12px;'>Please note this is an automated response email, please do not reply to this email address.</span></em></span><br />
	        <span style='color: rgb(105, 105, 105);'><em><span style='font-size: 12px;'>VAT Number 110 5820 61.</span></em></span></p>";

        #endregion


        public static void SendMail(string eto, string subject, string message)
        {
            try
            {

                var priority = MailPriority.Normal;

                MailMessage mailer = new MailMessage("info@espweb2print.co.uk", eto, subject, message);

                SmtpClient smtp = new SmtpClient("espcolour-co-uk.mail.protection.outlook.com");
                mailer.IsBodyHtml = true;
                mailer.Priority = priority;
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = null;
                smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtp.Send(mailer);

            }
            catch (Exception ex)
            {
                //LOG IT TO A LOG FILE
                Logger.WriteLog("Message " + message + " Mail has failed to send with error: " + ex);
            }

        }
    }
}
