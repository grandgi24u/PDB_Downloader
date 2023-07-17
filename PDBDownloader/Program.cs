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
using System.Data.SqlClient;

namespace PDBDownloader
{
    class Program
    {
        static string filePath = ConfigurationManager.AppSettings["outDir"];
        static List<ResultItem> fileLists;
        static ManageFile<ResultItem> file;
        static HttpClient client = new HttpClient(new HttpClientHandler { UseDefaultCredentials = false });
        static bool useSql = bool.Parse(ConfigurationManager.AppSettings["useSql"]);
        static SqlConnection conn;

        static async Task Main(string[] args)
        {
            string nbRow = ConfigurationManager.AppSettings["numberToCheck"];
            PrintParam();
            if (useSql)
            {
                await LaunchForSQl(nbRow);
            }
            else
            {
                await LaunchForFiles(nbRow);
            }
            Console.WriteLine("\nFinish !");
            Console.ReadLine();
        }

        // SECTION FOR SQL //

        static async Task LaunchForSQl(string nbRow)
        {
            Program.conn = new SqlConnection(ConfigurationManager.AppSettings["sqlConnection"]);
            Program.fileLists = new List<ResultItem>();
            Program.conn.Open();
            await GetBaseDataForSQL(0, int.Parse(nbRow), int.Parse(nbRow));
            StringBuilder insertQuery = new StringBuilder();
            SqlCommand command = conn.CreateCommand();
            foreach (ResultItem workon in Program.fileLists)
            {
                insertQuery.Append($"('{workon.filename}', {workon.clashscore.ToString().Replace(',', '.')}, '{workon.struct_pdbx_descriptors.Replace("'", "")}', '{workon.method}'),");
                //InsertAtomsIntoDatabase(await GetAtoms(workon.filename), workon.filename);
            }
            insertQuery.Length--;
            command.CommandText = "INSERT INTO Files (filename, clashscore, struct_pdbx_descriptors, method) VALUES " + insertQuery.ToString();
            await command.ExecuteNonQueryAsync();
            Program.conn.Close();
            return;
        }

        static async Task GetBaseDataForSQL(int start, int nbRow, int totalNouveauxObjets)
        {
            string apiUrl = ParseUrl(start, nbRow);
            var responseString = await client.GetStringAsync(apiUrl);
            var response = JsonConvert.DeserializeObject<Response>(responseString);
            var tasks = new List<Task>();
            int nouveauxObjetsCount = 0;
            SqlCommand command = conn.CreateCommand();
            foreach (ResponseItem item in response.result_set)
            {
                command.CommandText = "SELECT id FROM Files WHERE filename = '" + item.identifier + "'";
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        var workon = new ResultItem
                        {
                            filename = item.identifier
                        };
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
                await GetBaseDataForSQL(newStart, newNbRow, remainingNouveauxObjets);
            }
        }

        static async Task<List<Atom>> GetAtoms(string filename)
        {
            string apiUrl = $"https://files.rcsb.org/download/{filename}.cif";
            var responseString = await client.GetStringAsync(apiUrl).ConfigureAwait(false);
            var atoms = ParseAtomsFromContent(responseString, filename);
            return atoms;
        }

        static List<Atom> ParseAtomsFromContent(string content, string filename)
        {
            List<Atom> atoms = new List<Atom>();
            return atoms;
        }

        static void InsertAtomsIntoDatabase(List<Atom> atoms, string filename)
        {
            using (SqlCommand command = conn.CreateCommand())
            {
                foreach (var atom in atoms)
                {
                    command.CommandText = "INSERT INTO Atoms (atomId, type_symbol, label_atom_id, label_comp_id, Cartn_x, Cartn_y, Cartn_z, occupancy, B_iso_or_equiv, Id_File) VALUES " +
                        $"('{atom.atomId}', '{atom.type_symbol}', '{atom.label_atom_id}', '{atom.label_comp_id}', {atom.Cartn_x}, {atom.Cartn_y}, {atom.Cartn_z}, {atom.occupancy}, {atom.B_iso_or_equiv}, {filename})";
                    command.ExecuteNonQuery();
                }
            }
        }


        // SECTION FOR FILES //

        static async Task LaunchForFiles(string nbRow)
        {
            Console.WriteLine("\nStarting process please wait.");
            file = new ManageFile<ResultItem>(Path.Combine(Program.filePath, ConfigurationManager.AppSettings["outFileName"] + ".json"));
            Program.fileLists = new List<ResultItem>();
            await GetBaseData(0, int.Parse(nbRow), int.Parse(nbRow));
            Program.file.WriteObjects(Program.fileLists);
            Console.WriteLine("\nDo you want to generate the file of best file (required to download) ? (y/n)");
            nbRow = Console.ReadLine().ToLower().Trim();
            if (nbRow == "y")
            {
                KeepBestData();
                Console.WriteLine("\nDo you want to download all the files of best file ? (y/n)");
                long res = GetFullSizeOfBest();
                Console.WriteLine("! Warning ! It need " + res + " ko | " + (res/1024) + " Mo of free space !");
                nbRow = Console.ReadLine().ToLower().Trim();
                if (nbRow == "y")
                {
                    await DownloadFilesAsync();
                }
            }

            return;
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
                if (!existingObjects.Any(objet => objet.filename == item.identifier))
                {
                    var workon = new ResultItem
                    {
                        filename = item.identifier,
                        size = 0
                    };
                    var clashScoreTask = GetClashScore(workon);
                    var macroMoleTask = GetMacroMole(workon);
                    //var sizeTask = GetSizeOfFile(workon);
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

        static async Task DownloadFilesAsync()
        {
            string bestFileName = Program.filePath + ConfigurationManager.AppSettings["bestFileName"] + ".json";
            if (!File.Exists(bestFileName))
            {
                return;
            }
            ManageFile<ResultItem> bestFile = new ManageFile<ResultItem>(bestFileName);
            foreach (ResultItem item in bestFile.FindObjects())
            {
                string outputDir = ConfigurationManager.AppSettings["outDirForDownload"] + item.filename + ".mmcif";
                string apiUrl = "https://files.rcsb.org/download/" + item.filename + ".cif";
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                byte[] fileContent = await response.Content.ReadAsByteArrayAsync();
                File.WriteAllBytes(outputDir, fileContent);
                Console.WriteLine("Download with success : " + item.filename + ".mmcif");
            }
            
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

        // COMMON SECTION //

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

        static async Task<ResultItem> GetSizeOfFile(ResultItem file)
        {
            string apiUrl = "https://files.rcsb.org/download/" + file.filename + ".cif";
            HttpResponseMessage response = await client.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength.HasValue)
            {
                file.size = response.Content.Headers.ContentLength.Value / 1024;
                return file;
            }
            else
            {
                throw new Exception("File size information not available.");
            }
        }

        static long GetFullSizeOfBest()
        {
            string bestFileName = Program.filePath + ConfigurationManager.AppSettings["bestFileName"] + ".json";
            if (!File.Exists(bestFileName))
            {
                return 0;
            }
            ManageFile<ResultItem> bestFile = new ManageFile<ResultItem>(bestFileName);
            List<ResultItem> liRes = bestFile.FindObjects();
            long res = 0;
            foreach(ResultItem item in liRes)
            {
                res += item.size;
            }
            return res;
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

        static void PrintParam()
        {
            Console.WriteLine("Program PDBDownload developped by Clément GRANDGIRARD");
            Console.WriteLine("\nActual parameter (can be change in App.config) :");
            Console.WriteLine("\n   Number of file to check : " + ConfigurationManager.AppSettings["numberToCheck"]);
            Console.WriteLine("   Use a SQL server database : " + Program.useSql);
            if (useSql)
            {
                Console.WriteLine("   SQL server link : " + ConfigurationManager.AppSettings["sqlConnection"]);
            }
            else
            {
                Console.WriteLine("   File output folder : " + ConfigurationManager.AppSettings["outDir"]);
                Console.WriteLine("   Downloaded file output folder : " + ConfigurationManager.AppSettings["outDirForDownload"]);
            }
        }
    }
}
