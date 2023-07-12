using Newtonsoft.Json;
using PDBDownloader.Models;
using PDBDownloader.Controllers;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace PDBDownloader
{
    class Program
    {
        static string filePath = ConfigurationManager.AppSettings["outDir"];
        static List<ResultItem> fileLists;
        static ManageFile<ResultItem> file;
        static HttpClient client;

        static async Task Main(string[] args)
        {
            Console.WriteLine("How many file do you want to try (Recommanded 50) : ");
            string nbRow = Console.ReadLine();
            file = new ManageFile<ResultItem>(Path.Combine(Program.filePath, ConfigurationManager.AppSettings["outFileName"] + ".json"));
            Program.fileLists = new List<ResultItem>();
            client = new HttpClient(new HttpClientHandler { UseDefaultCredentials = false });
            await GetBaseData(0, int.Parse(nbRow), int.Parse(nbRow));
            Program.file.WriteObjects(Program.fileLists);
            Console.WriteLine("\nDo you want to generate the file of best file ? (y/n)");
            nbRow = Console.ReadLine().ToLower().Trim();
            if (nbRow == "y")
            {
                KeepBestData();
            }
            Console.WriteLine("\nFinish !");
            Console.ReadLine();
        }

        static async Task GetBaseData(int start, int nbRow, int totalNouveauxObjets)
        {
            string apiUrl = ParseUrl(start, nbRow);
            var responseString = await client.GetStringAsync(apiUrl);
            var response = JsonConvert.DeserializeObject<Response>(responseString);
            List<ResultItem> existingObjects = Program.file.FindObjects();
            int nouveauxObjetsCount = 0;
            var tasks = new List<Task>();
            foreach (ResponseItem item in response.result_set)
            {
                var workon = new ResultItem
                {
                    filename = item.identifier
                };
                if (!existingObjects.Any(objet => objet.filename == workon.filename))
                {
                    var clashScoreTask = GetClashScore(workon);
                    var macroMoleTask = GetMacroMole(workon);
                    tasks.Add(Task.WhenAll(clashScoreTask, macroMoleTask).ContinueWith(_ =>
                    {
                        Program.fileLists.Add(workon);
                        nouveauxObjetsCount++;
                        if (nouveauxObjetsCount == totalNouveauxObjets)
                        {
                            return;
                        }
                    }));
                }
            }
            Task t = Task.WhenAll(tasks);
            try
            {
                t.Wait();
            }
            catch { }
            int remainingNouveauxObjets = totalNouveauxObjets - nouveauxObjetsCount;
            if (remainingNouveauxObjets > 0)
            {
                int newStart = start + nbRow;
                int newNbRow = Math.Min(nbRow, remainingNouveauxObjets);
                await GetBaseData(newStart, newNbRow, remainingNouveauxObjets);
            }
        }

        static async Task<ResultItem> GetClashScore(ResultItem file)
        {
            string url = "https://files.rcsb.org/pub/pdb/validation_reports/" + file.filename[1].ToString().ToLower() + file.filename[2].ToString().ToLower() + "/" + file.filename.ToLower() + "/" + file.filename.ToLower() + "_validation.xml.gz";
            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            using (Stream responseStream = await response.Content.ReadAsStreamAsync())
            using (GZipStream gzipStream = new GZipStream(responseStream, CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(gzipStream, Encoding.UTF8))
            {
                string uncompressedContent = reader.ReadToEnd();
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(uncompressedContent);
                XmlNode entryNode = xmlDoc.SelectSingleNode("//Entry");
                string clashscore = entryNode?.Attributes["clashscore"]?.Value;
                if (clashscore == "" || clashscore == null)
                    clashscore = null;
                else
                    file.clashscore = double.Parse(clashscore.Trim().Replace('.', ','));
            }
            return file;
        }

        static async Task<ResultItem> GetMacroMole(ResultItem file)
        {
            string apiUrl = "https://data.rcsb.org/rest/v1/core/entry/" + file.filename;
            HttpResponseMessage response = await client.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();
            string jsonResponse = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject(jsonResponse);
            if (result["struct"].pdbx_descriptor != null)
            {
                file.struct_pdbx_descriptors = result["struct"].pdbx_descriptor;
            }
            else
            {
                if (result["struct_keywords"].pdbx_keywords != null)
                {
                    file.struct_pdbx_descriptors = result["struct_keywords"].pdbx_keywords;
                }
            }
            if (result["exptl"][0].method != null)
            {
                file.method = result["exptl"][0].method;
            }
            return file;
        }

        static async Task DownloadFileAsync(string name)
        {
            string outputDir = Program.filePath + name + ".cif";
            string apiUrl = "https://files.rcsb.org/download/" + name + ".cif";
            HttpResponseMessage response = await client.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();
            byte[] fileContent = await response.Content.ReadAsByteArrayAsync();
            File.WriteAllBytes(outputDir, fileContent);
            Console.WriteLine("Download with success : " + name + ".cif");
        }

        static void KeepBestData()
        {
            string bestFileName = Program.filePath + ConfigurationManager.AppSettings["bestFileName"] + ".json";
            if (File.Exists(bestFileName))
            {
                File.Delete(bestFileName);
            }
            ManageFile<ResultItem> bestFile = new ManageFile<ResultItem>(bestFileName);
            List<string> alreadyChecked = new List<string>();
            List<ResultItem> toCompare = new List<ResultItem>();
            List<ResultItem> bestFileList = new List<ResultItem>();
            ResultItem bestItem;
            foreach (ResultItem item in file.FindObjects())
            {
                if (item.struct_pdbx_descriptors == null)
                {
                    bestFileList.Add(item);
                }
                else if (!alreadyChecked.Contains(item.struct_pdbx_descriptors))
                {
                    toCompare = file.FindObjectsByCondition(x => x.struct_pdbx_descriptors == item.struct_pdbx_descriptors);
                    if (toCompare.Count() == 1)
                    {
                        bestFileList.Add(item);
                    }
                    else
                    {
                        bestItem = item;
                        foreach (ResultItem item2 in toCompare)
                        {
                            if (item2.clashscore < bestItem.clashscore)
                            {
                                bestItem = item2;
                            }
                        }
                        bestFileList.Add(bestItem);
                    }
                    alreadyChecked.Add(item.struct_pdbx_descriptors);
                }
            }
            bestFile.WriteObjects(bestFileList);
        }

        static string ParseUrl(int start, int nb_row)
        {
            var queryObject = new
            {
                query = new
                {
                    type = "terminal",
                    service = "text"
                },
                request_options = new
                {
                    paginate = new
                    {
                        start = start,
                        rows = nb_row
                    }
                },
                return_type = "entry"
            };
            try
            {
                return "https://search.rcsb.org/rcsbsearch/v2/query?json=" + Uri.EscapeDataString(JsonConvert.SerializeObject(queryObject));
            }
            catch (Exception ex)
            {
                return "Error : " + ex.Message;
            }
        }
    }
}
