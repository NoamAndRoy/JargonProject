using LumenWorks.Framework.IO.Csv;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace AddUKUSWords
{
    class Program
    {
        static void Main(string[] args)
        {
            var lines = loadUSUKLines("Words-Worldwide-Word-list-UK-US-2009.docx");
            var instancesMatrixGeneral16 = loadInstancesMatrix("Data2016.csv");
            var instancesMatrixGeneral17 = loadInstancesMatrix("Data2017.csv");
            //var instancesMatrixGeneral18p1 = loadInstancesMatrix("Data2018p1.csv");
            var instancesMatrixGeneral18p2 = loadInstancesMatrix("Data2018p2.csv");
            var instancesMatrixGeneral19 = loadInstancesMatrix("Data2019.csv");

            var instanceMatrix = mergeMatrices(instancesMatrixGeneral18p2,
                                                        mergeMatrices(instancesMatrixGeneral19,
                                                            mergeMatrices(instancesMatrixGeneral16, instancesMatrixGeneral17)));
            UpdateInstancesMatrix(lines, instanceMatrix);
            writeCSV(instanceMatrix, "DataUKUS2016-2019.csv");
        }

        public static Dictionary<string, int> mergeMatrices(Dictionary<string, int> i_Matrix1, Dictionary<string, int> i_Matrix2)
        {
            Dictionary<string, int> mergedMatrix = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> pair in i_Matrix2)
            {
                if (mergedMatrix.ContainsKey(pair.Key.ToLower()))
                {
                    mergedMatrix[pair.Key.ToLower()] += pair.Value;
                }
                else
                {
                    mergedMatrix.Add(pair.Key.ToLower(), pair.Value);
                }
            }

            foreach (KeyValuePair<string, int> pair in i_Matrix1)
            {
                if (mergedMatrix.ContainsKey(pair.Key.ToLower()))
                {
                    mergedMatrix[pair.Key.ToLower()] += pair.Value;
                }
                else
                {
                    mergedMatrix.Add(pair.Key.ToLower(), pair.Value);
                }
            }

            return mergedMatrix;
        }

        public static void writeCSV(Dictionary<string, int> i_Dict, string i_Path)
        {
            StreamWriter sw = File.CreateText(i_Path);

            foreach (KeyValuePair<string, int> pair in i_Dict.OrderByDescending(pair => pair.Value))
            {
                sw.Write(pair.Key + "," + pair.Value + "\r\n");
            }

            sw.Close();
        }

        public static void UpdateInstancesMatrix(List<string> i_Lines, Dictionary<string, int> i_InstancesMatrix)
        {
            List<string> notContained = new List<string>();
            string[] words;
            int max;

            foreach (string line in i_Lines)
            {
                words = line.Split('\t').Distinct().ToArray();
                max = -1;

                for (int i = 0; i < words.Length; i++)
                {
                    if (i_InstancesMatrix.ContainsKey(words[i]))
                    {
                        if (max < i_InstancesMatrix[words[i]])
                        {
                            max = i_InstancesMatrix[words[i]];
                        }
                        else
                        {
                            notContained.Add(words[i]);
                        }
                    }
                    else if (words[i] != "")
                    {
                        notContained.Add(words[i]);
                    }
                }

                for (int i = 0; max != -1 && i < notContained.Count; i++)
                {
                    i_InstancesMatrix[notContained[i]] = max;
                }

                notContained.Clear();
            }
        }

        public static List<string> loadUSUKLines(string i_Path)
        {
            List<string> lines = new List<string>();

            using (FileStream stream = new FileStream(i_Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                DocX document = DocX.Load(stream);

                foreach (Paragraph item in document.Paragraphs)
                {
                    lines.Add(item.Text);
                }
            }

            return lines;
        }

        public static Dictionary<string, int> loadInstancesMatrix(string i_Path)
        {
            Dictionary<string, int> wordList = new Dictionary<string, int>();

            TextReader data = new StreamReader(new FileStream(i_Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            var csv = new CsvReader(data, false);

            wordList.Clear();

            while (csv.ReadNextRecord())
            {
                wordList.Add(csv[0], int.Parse(csv[1]));
            }

            data.Close();

            return wordList;
        }
    }
}
