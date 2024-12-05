using System;
using System.IO;
using System.Web;

namespace JargonProject.Handlers
{
    public sealed class Logger
    {
        private readonly string r_LogFile = HttpContext.Current.Server.MapPath(@"~\Logs\Log.txt").ToString();

        private static Logger s_Instance;
        private static object s_LockObj = new object();
        
        public static Logger Instance
        {
            get
            {

                if (s_Instance == null)
                {
                    s_Instance = new Logger();
                }

                return s_Instance;
            }
        }

        public void Write(string i_Value, params object[] i_parameters)
        {
            using (StreamWriter stream = new StreamWriter(new FileStream(r_LogFile, FileMode.Append)))
            {
                stream.Write(DateTime.Now.ToString() + ": " + i_Value, i_parameters);
            }
        }

        public void WriteLine(string i_Value, params object[] i_parameters)
        {
            using (StreamWriter stream = new StreamWriter(new FileStream(r_LogFile, FileMode.Append)))
            {
                stream.WriteLine(DateTime.Now.ToString() + ": " + i_Value, i_parameters);
            }
        }

        public string ReadLine()
        {
            using (StreamReader stream = new StreamReader(new FileStream(r_LogFile, FileMode.Open, FileAccess.Read)))
            {
                return stream.ReadLine();
            }
        }

        public string ReadAll()
        {
            using (StreamReader stream = new StreamReader(new FileStream(r_LogFile, FileMode.Open, FileAccess.Read)))
            {
                return stream.ReadToEnd();
            }
        }

        private static object s_Lock = new object();

        public static void UpdateNumberOfUses(int i_AmountOfUses)
        {
            lock (s_Lock)
            {
                //string fileLocation = HttpContext.Current.Server.MapPath(@"~\Logs\AmountOfUses.txt");
                string fileLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Logs\AmountOfUses.txt");

                ulong numberOfUses = !File.Exists(fileLocation) ? 0 : ulong.Parse(File.ReadAllText(fileLocation).Replace(",", ""));

                numberOfUses += Convert.ToUInt64(i_AmountOfUses);

                using (StreamWriter file = new StreamWriter(fileLocation, false))
                {
                    file.Write(numberOfUses);
                }
            }
        }

        public static ulong ReadAmountOfUses()
        {
            //string fileLocation = HttpContext.Current.Server.MapPath(@"~\Logs\AmountOfUses.txt");
            string fileLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Logs\AmountOfUses.txt");
            ulong numberOfUses = 0;

            if(File.Exists(fileLocation))
            {
                using (FileStream stream = File.Open(fileLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        while (!reader.EndOfStream)
                        {
                            numberOfUses = ulong.Parse(reader.ReadToEnd().Replace(",", ""));
                        }
                    }
                }
            }
            
            return numberOfUses;
        }
    }
}