using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Globalization;
using System.Linq;

public class ArduinoDataLoader : DataLoaderBase
{
    [SerializeField]
    public List<TimeSeries> timeSeriesList;
    public LoadTimeSeries timeSeriesHandeler;

    public override void LoadTimeSeriesFromFolder(string folderPath, Action<List<TimeSeries>> callback)
    {
        StartCoroutine(LoadTimeSeriesFromFolderCoroutine(folderPath, callback));
    }

    public IEnumerator LoadTimeSeriesFromFolderCoroutine(string folderPath, Action<List<TimeSeries>> callback)
    {
        timeSeriesList = new List<TimeSeries>();

        string[] filePaths = null;

        try
        {
            filePaths = Directory.GetFiles(folderPath, "*.csv");
        }
        catch (Exception e)
        {
            Debug.LogError("An error occurred while accessing the directory: " + e.Message);
            callback?.Invoke(null);
            yield break;
        }

        timeSeriesHandeler.InstantiateProgressBar("Loading");
        int totalFiles = filePaths.Length;
        int currentFileNumber = 0;
        foreach (var filePath in filePaths)
        {
            currentFileNumber += 1;
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string[] nameParts = fileName.Split('-');
            if (nameParts.Length != 2)
            {
                Debug.LogError("File name format is incorrect: " + fileName);
                continue;
            }

            var sensorName = nameParts[0];
            var key = nameParts[1];

            TimeSeries timeSeries = timeSeriesList.FirstOrDefault(t => t.sensorName == sensorName);
            if (timeSeries == null)
            {
                timeSeries = new TimeSeries();
                timeSeries.sensorName = sensorName;
                timeSeries.data = new List<TimeSeriesData>();
                timeSeriesList.Add(timeSeries);
            }

            TimeSeriesData timeSeriesData = timeSeries.data.FirstOrDefault(d => d.key == key);
            if (timeSeriesData == null)
            {
                timeSeriesData = new TimeSeriesData();
                timeSeriesData.key = key;
                timeSeriesData.measuredAt = new List<DateTime>();
                timeSeriesData.value = new List<object>();
                timeSeries.data.Add(timeSeriesData);
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath);
            }
            catch (Exception e)
            {
                Debug.LogError("An error occurred while accessing the file: " + e.Message);    
                timeSeriesHandeler.DestroyMenu(ref timeSeriesHandeler.progressBarInstanse);
                callback?.Invoke(null);
                yield break;
            }

            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length != 2)
                {
                    Debug.LogError("Line format is incorrect: " + line);
                    continue;
                }

                DateTime date;
                if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date))
                {
                    Debug.LogError("Could not parse date: " + parts[0]);
                    continue;
                }

                if (bool.TryParse(parts[1], out bool boolVal))
                {
                    timeSeriesData.value.Add(boolVal);
                }
                else if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float measurement))
                {
                    timeSeriesData.value.Add(measurement);
                }
                else
                {
                    Debug.LogError("Could not parse measurement: " + parts[1]);
                    continue;
                }

                timeSeriesData.measuredAt.Add(date);
            }

            timeSeriesHandeler.UpdateProgressBar((float)(currentFileNumber + 1) / totalFiles);

            yield return null;
        }

        timeSeriesHandeler.DestroyMenu(ref timeSeriesHandeler.progressBarInstanse);
        callback?.Invoke(timeSeriesList);
    }
}