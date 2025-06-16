using System.Net.Mail;
using System.Net;

namespace DNTS_CLIS.Models
{
    public class EmailService
    {
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(string smtpServer, int smtpPort, string smtpUsername, string smtpPassword, string fromEmail, string fromName)
        {
            _smtpServer = smtpServer;
            _smtpPort = smtpPort;
            _smtpUsername = smtpUsername;
            _smtpPassword = smtpPassword;
            _fromEmail = fromEmail;
            _fromName = fromName;
        }


        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string username, string temporaryPassword)
        {
            try
            {
                using (var client = new SmtpClient(_smtpServer, _smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);

                    var from = new MailAddress(_fromEmail, _fromName, System.Text.Encoding.UTF8);
                    var to = new MailAddress(toEmail);

                    using (var message = new MailMessage(from, to))
                    {
                        message.Subject = "Password Reset - DNTS CLIS System";
                        message.SubjectEncoding = System.Text.Encoding.UTF8;

                        message.Body = $@"
                            <html>
                            <body>
                                <h2>Password Reset Request</h2>
                                <p>Hello {username},</p>
                                <p>You have requested a password reset for your DNTS CLIS account.</p>
                                <p><strong>Your temporary password is: {temporaryPassword}</strong></p>
                                <p>Please use this temporary password to log in and change your password immediately.</p>
                                <p>If you did not request this password reset, please contact your system administrator.</p>
                                <br>
                                <p>Best regards,<br>DNTS CLIS System</p>
                            </body>
                            </html>";

                        message.BodyEncoding = System.Text.Encoding.UTF8;
                        message.IsBodyHtml = true;

                        await client.SendMailAsync(message);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email sending failed: {ex.Message}");
                return false;
            }
        }
    }
}

