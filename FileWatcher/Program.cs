using IAPL.Transport.Transactions;
using System;
using System.IO;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Linq;
public class Watcher
{

    public static void Main()
    {
        Run();
    }

    public static void Run()
    {
        int workerThreads, complete;
        ThreadPool.GetMaxThreads(out workerThreads, out complete);
        bool success                = ThreadPool.SetMinThreads(workerThreads, complete);
        FileSystemWatcher watcher   = new FileSystemWatcher();
        watcher.Path                = @"\\WLAP0135\Shared\MessageCodeXMLs";
        watcher.NotifyFilter        = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        watcher.Filter              = "*.xml";
        watcher.Created            += new FileSystemEventHandler(OnChanged);
        watcher.EnableRaisingEvents = true;
        Console.WriteLine("Press \'q\' to quit the sample.");
        while (Console.Read() != 'q') ;
    }

    private static void OnChanged(object source, FileSystemEventArgs e)
    {
        Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);

        WaitReady(e.FullPath);

        ServerSerializationDetails serverSerializationDetails = null;
        string path                                           = e.FullPath;
        XmlSerializer serializer                              = new XmlSerializer(typeof(ServerSerializationDetails));
        StreamReader reader                                   = new StreamReader(path);
        serverSerializationDetails                            = (ServerSerializationDetails)serializer.Deserialize(reader);
        reader.Close();
        
        //sequential approach
        //foreach (string item in serverSerializationDetails.SourceServerDetails.Files)
        //{
        //    Process(item, serverSerializationDetails);
        //}

        //parallel approach

        try
        {
            Parallel.ForEach(serverSerializationDetails.SourceServerDetails.Files.AsParallel(),
                             new ParallelOptions { MaxDegreeOfParallelism = -1 },
                             item => Task.Factory.StartNew(() => Process(item, serverSerializationDetails))
                        );
        }
        catch (Exception ex)
        {
            Console.WriteLine("=================" + ex.Message + "=================");
        }
        finally
        {
            File.Delete(e.FullPath);
        }
    }

    public static void WaitReady(string fileName)
    {
        while (true)
        {
            try
            {
                using (Stream stream = System.IO.File.Open(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    if (stream != null)
                    {
                        Console.WriteLine(string.Format("Output file {0} ready.", fileName));
                        break;
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine(string.Format("Output file {0} not yet ready ({1})", fileName, ex.Message));
            }
            catch (IOException ex)
            {
                Console.WriteLine(string.Format("Output file {0} not yet ready ({1})", fileName, ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine(string.Format("Output file {0} not yet ready ({1})", fileName, ex.Message));
            }
            Thread.Sleep(500);
        }
    }

    private static void Process(string item, ServerSerializationDetails serverSerializationDetails)
    {
        Worker worker = new Worker(serverSerializationDetails.SourceServerDetails.ThreadName
                                     , item
                                     , serverSerializationDetails.SourceServerDetails.MessageDetails
                                     , serverSerializationDetails.SourceServerDetails
                                     , serverSerializationDetails.DestinationServerDetails
                                     , 1500
                                     , 5);
        worker.ProcessFile();
    }

}