using JargonProject.Handlers;
using LumenWorks.Framework.IO.Csv;
using System;
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
            GenerateDataDict(2012, 2015);
            GenerateDataDict(2013, 2016);
            GenerateDataDict(2014, 2017);
            GenerateDataDict(2015, 2018);
            GenerateDataDict(2016, 2019);
            GenerateDataDict(2017, 2020);
            GenerateDataDict(2018, 2021);
            GenerateDataDict(2019, 2022);
            GenerateDataDict(2020, 2023);
            //CompareDicts();
        }

        public static void CompareDicts()
        {
            string folderPath = @"F:\royosef\projects\JargonProject\GeneralSpider\article_files_2018";

            string[] textFiles = Directory.GetFiles(folderPath, "*.txt");

            var results = new Dictionary<string, int>();
            TextGrading.Lang = Language.English2016_2019;

            int i = 0;

            foreach (string filePath in textFiles)
            {
                i++;
                Console.WriteLine($"{i}/{textFiles.Length}");
                string textContent = File.ReadAllText(filePath);

                if (string.IsNullOrEmpty(textContent))
                { 
                    results[filePath] = -1;
                }
                else
                {
                    var a = TextGrading.AnalyzeSingleText(textContent);

                    results[filePath] = a.Score;
                }

                if (i == 5) break;
            }

            TextGrading.Lang = Language.English2016_2019_2024;

            foreach (string filePath in results.Keys)
            {
                string textContent = File.ReadAllText(filePath);

                if (string.IsNullOrEmpty(textContent))
                {
                    Console.WriteLine($"Processed file: {filePath}, Empty");
                }
                else
                {
                    var b = TextGrading.AnalyzeSingleText(textContent);

                    var value1 = results[filePath];
                    var value2 = b.Score;
                    var difference = value1 - value2;
                    var percentage_difference = value1 != 0 ? (difference / value1) * 100 : 0;

                    Console.WriteLine($"Processed file: {filePath}, {value1}, {value2}, {difference}, {percentage_difference}");
                }
            }



            Console.WriteLine("All files have been processed.");
        }

        public static void GenerateDataDict(int start, int end)
        {
            var lines = loadUSUKLines("Words-Worldwide-Word-list-UK-US-2009.docx");

            var instanceMatrix = loadInstancesMatrix($"GeneralData_{start}.csv");

            for(int i = start+1; i <= end; i++)
            {
                instanceMatrix = mergeMatrices(instanceMatrix, loadInstancesMatrix($"GeneralData_{i}.csv"));
            }

            UpdateInstancesMatrix(lines, instanceMatrix);
            writeCSV(instanceMatrix, $"T_2024DataUKUS{start}-{end}.csv");
        }

        public static void GenerateDataDict()
        {
            var lines = loadUSUKLines("Words-Worldwide-Word-list-UK-US-2009.docx");
            var instancesMatrixGeneral16 = loadInstancesMatrix("GeneralData_2016.csv");
            var instancesMatrixGeneral17 = loadInstancesMatrix("GeneralData_2017.csv");
            var instancesMatrixGeneral18 = loadInstancesMatrix("GeneralData_2018.csv");
            var instancesMatrixGeneral19 = loadInstancesMatrix("GeneralData_2019.csv");

            var instanceMatrix = mergeMatrices(instancesMatrixGeneral18,
                                                        mergeMatrices(instancesMatrixGeneral19,
                                                            mergeMatrices(instancesMatrixGeneral16, instancesMatrixGeneral17)));
            UpdateInstancesMatrix(lines, instanceMatrix);
            writeCSV(instanceMatrix, "2024DataUKUS2016-2019.csv");
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
