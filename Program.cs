using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Mail;

namespace BankTransferChecker
{
    class Program
    {
        static void Main(string[] args)
        {
            Boolean isError = false;
            try
            {
                LogActions("Begin Bank Transfer Check", true);
                //*********************************************************************************
                //************Check for existence of Outbound file in Archive*********************
                //*********************************************************************************
                DateTime today = DateTime.Today;
                string inboundDir = ConfigurationManager.AppSettings["inboundFolder"];
                string outboundDir = ConfigurationManager.AppSettings["outboundFolder"];
                string archiveDir = ConfigurationManager.AppSettings["archiveFolder"];
                string fileSnippet = ConfigurationManager.AppSettings["fileSnippet"];
                int firstRunTime = Convert.ToInt32(ConfigurationManager.AppSettings["firstRunTime"]);
                int secondRunTime = Convert.ToInt32(ConfigurationManager.AppSettings["secondRunTime"]);
                Boolean isDebug = Convert.ToBoolean(ConfigurationManager.AppSettings["debug"]);

                if (!checkForAcknowledgement(inboundDir))
                {
                    LogActions("Acknowledgement file does not exist", false);
                    DirectoryInfo di = new DirectoryInfo(archiveDir);
                    var files = di.GetFiles("*", SearchOption.TopDirectoryOnly);
                    List<TxfrFiles> todaysFiles = new List<TxfrFiles>();
                    TxfrFiles txFiles = new TxfrFiles();
                    int cntr = 0;
                    foreach (FileInfo d in files)
                    {
                        if (DateTime.Now.ToString("dd/MM/yy") == d.LastWriteTime.ToString("dd/MM/yy") && (d.Name.Substring(0, 26).Contains(fileSnippet)))
                        {
                            cntr++;
                            txFiles.orgArchiveFile = d.FullName;
                            txFiles.outBoundFile = d.Name.Substring(17, 13);
                            todaysFiles.Add(txFiles);
                        }
                    }
                    if (cntr > 0)
                    {
                        LogActions(cntr + " Archive file(s) exist(s)", false);
                        DateTime localTime = DateTime.Now;
                        if (localTime.Hour == firstRunTime)
                        {
                            foreach (TxfrFiles f in todaysFiles)
                            {
                                //now copy the file path + filename to outboundDir
                                string outBoundFileName = outboundDir + "\\" + f.outBoundFile;
                                File.Copy(f.orgArchiveFile, outBoundFileName);
                                LogActions("Archive file " + f.outBoundFile + " copied to Outbound directory", false);
                            }
                        }
                        else if (localTime.Hour == secondRunTime)
                        {
                            isError = true;
                            SendMail("Second Acknowledgement file check failed", isError);
                            LogActions("Second Acknowledgement file failed. Sent error email", false);
                            LogErrors("Second Acknowledgement file failed. Sent error email");
                        }
                        else
                        {
                            isError = true;
                            SendMail("Second Acknowledgement file check failed", isError);
                            LogActions("Not in runtime window", false);
                        }
                    }
                    else
                    {
                        if (isDebug)
                            SendMail("No Errors", isError);
                        LogActions("Archive file does not exist - Exiting", false);
                    }
                }
                else
                {
                    LogActions("Acknowledgement file exists - Exiting", false);
                    if (isDebug)
                        SendMail("No Errors", isError);
                }

                LogActions("End Bank Transfer Check", false);
            }
            catch (Exception ex)
            {
                LogActions(ex.Message, false);
                LogErrors(ex.Message);
                LogErrors(ex.StackTrace);
                LogActions("End Bank Transfer Check", false);
                isError = true;
                SendMail(ex.Message, isError);
            }
        }

        static Boolean checkForAcknowledgement(string indir)
        {
            Exception BankCheckerAckException = new Exception("BankCheckerAckException");
            Boolean exists = false;
            try
            {
                DirectoryInfo di = new DirectoryInfo(indir);
                var files = di.GetFiles("*", SearchOption.TopDirectoryOnly);
                foreach (FileInfo d in files)
                {
                    if (DateTime.Now.ToString("dd/MM/yy") == d.LastWriteTime.ToString("dd/MM/yy"))
                        exists = true;
                }
            }
            catch (Exception ex)
            {
                BankCheckerAckException.Source = ex.Message;
                throw BankCheckerAckException;
            }
            return exists;
        }
        static void LogActions(string logMessage, bool logDate)
        {
            Exception BankCheckerActionException = new Exception("BankCheckerActionException");
            try
            {
                string actionLog = ConfigurationManager.AppSettings["actionLog"];
                string archiveActionLog = string.Empty;
                string filePath = string.Empty;
                string eom = checkEOM();
                if (eom != "NOT_EOM" && logDate) //LogDate also acts as an idicator that the entry is the first made today.
                {
                    filePath = Path.GetDirectoryName(actionLog);
                    archiveActionLog = filePath + "\\" + Path.GetFileNameWithoutExtension(actionLog) + eom + ".txt";
                    File.Move(actionLog, archiveActionLog);
                }
                using (StreamWriter w = File.AppendText(actionLog))
                {
                    if (logDate)
                    {
                        w.Write("====================================================================");
                        w.Write("\r\nLog Entry : ");
                        w.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString());
                    }

                    w.WriteLine("  :{0}", logMessage);
                    if (logMessage.IndexOf("End ", 0) == 0)
                        w.Write("====================================================================");
                    w.Close();
                }
            }
            catch (Exception ex)
            {
                BankCheckerActionException.Source = ex.Message;
                throw BankCheckerActionException;
            }
        }
        static void LogErrors(string logMessage)
        {
            Exception BankCheckerErrorException = new Exception("BankCheckerErrorException");
            try
            {
                string errorLog = ConfigurationManager.AppSettings["errorLog"];
                string archiveErrorLog = string.Empty;
                string filePath = string.Empty;
                string eom = checkEOM();
                if (eom != "NOT_EOM" && File.Exists(errorLog)) //LogDate also acts as an idicator that the entry is the first made today.
                {
                    filePath = Path.GetDirectoryName(errorLog);
                    archiveErrorLog = filePath + "\\" + Path.GetFileNameWithoutExtension(archiveErrorLog) + eom + ".txt";
                    File.Move(errorLog, archiveErrorLog);
                }
                using (StreamWriter w = File.AppendText(errorLog))
                {
                    w.Write("====================================================================");
                    w.Write("\r\nError : ");
                    w.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString());
                    w.WriteLine("  :{0}", logMessage);
                    w.Write("====================================================================");
                    w.Close();
                }
            }
            catch (Exception ex)
            {
                BankCheckerErrorException.Source = ex.Message;
                throw BankCheckerErrorException;
            }
        }
        static void SendMail(string msg, Boolean err)
        {
            Exception BankCheckerErrorException = new Exception("BankCheckerErrorException");

            try
            {
                MailAddress to = new MailAddress(ConfigurationManager.AppSettings["mailTo"]);
                MailAddress from = new MailAddress(ConfigurationManager.AppSettings["mailFrom"]);
                MailAddress cc = new MailAddress(ConfigurationManager.AppSettings["mailCC"]);
                MailMessage mail = new MailMessage(from, to);
                string chkSuccessFailure = string.Empty;
                mail.CC.Add(cc);
                if (err)
                    mail.Subject = ConfigurationManager.AppSettings["mailSubject"] + " FAILED";
                else
                    mail.Subject = ConfigurationManager.AppSettings["mailSubject"] + " SUCCEEDED";
                mail.Body = msg;

                SmtpClient smtp = new SmtpClient();
                smtp.Host = ConfigurationManager.AppSettings["smtpHost"];
                smtp.Port = Convert.ToInt32(ConfigurationManager.AppSettings["smtpPort"]); ;
                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                LogActions("Failed to send email", false);
                BankCheckerErrorException.Source = ex.Message;
                throw BankCheckerErrorException;
            }
        }
        static string checkEOM()
        {
            //Check if start to new month. If so, then move existing filename to new file name with date attached.
            string day = DateTime.Today.Day.ToString(); //dte.Day.ToString();
            string month = DateTime.Today.Month.ToString(); // dte.Month.ToString();
            string year = DateTime.Today.Year.ToString(); //dte.Year.ToString();
            if (day == "1")
            {
                if (month == "1")
                {
                    month = "12";
                    int yr = int.Parse(year);
                    yr = yr - 1;
                    year = yr.ToString("d4");
                }
                else
                {
                    int mth = int.Parse(month);
                    mth = mth - 1;
                    month = mth.ToString("d2");
                }
                return month + "-" + year;
            }
            else
                return "NOT_EOM";
        }
    }
}
