using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDBDownloader.Controllers
{
    class ManageFile<T>
    {
        private string fileName;

        public ManageFile(string fileName)
        {
            this.fileName = fileName;
            this.CreateFile();
        }

        public void WriteObjects(List<T> objets)
        {
            string contenuFichier = File.ReadAllText(this.fileName);
            List<T> objetsExistant = JsonConvert.DeserializeObject<List<T>>(contenuFichier);
            objetsExistant.AddRange(objets);
            string json = JsonConvert.SerializeObject(objetsExistant, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(this.fileName, json);
        }

        public List<T> FindObjects()
        {
            string json = File.ReadAllText(this.fileName);
            return JsonConvert.DeserializeObject<List<T>>(json);
        }

        public T FindObject(Func<T, bool> condition)
        {
            List<T> objets = FindObjects();

            foreach (T objet in objets)
            {
                if (condition(objet))
                {
                    return objet;
                }
            }

            return default(T);
        }

        public List<T> FindObjectsByCondition(Func<T, bool> condition)
        {
            List<T> objets = FindObjects();
            List<T> res = new List<T>();

            foreach (T objet in objets)
            {
                if (condition(objet))
                {
                    res.Add(objet);
                }
            }

            return res;
        }

        public void CreateFile()
        {
            if (!File.Exists(this.fileName))
            {
                File.Create(this.fileName).Close();
                string json = JsonConvert.SerializeObject(new List<T>(), Newtonsoft.Json.Formatting.Indented);
                using (StreamWriter writer = new StreamWriter(fileName, false))
                {
                    writer.WriteLine(json);
                }
            }
        }
    }
}
