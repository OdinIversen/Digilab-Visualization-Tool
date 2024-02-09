using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public interface IDataLoader
{
    void LoadTimeSeriesFromFolder(string folderPath, Action<List<TimeSeries>> callback);
}

[System.Serializable]
public class TimeSeries
{
    public string sensorName;
    public List<TimeSeriesData> data;
}

[System.Serializable]
public class TimeSeriesData
{
    public string key;
    public List<DateTime> measuredAt;
    public List<object> value;
}

public abstract class DataLoaderBase : MonoBehaviour, IDataLoader
{
    public abstract void LoadTimeSeriesFromFolder(string folderPath, Action<List<TimeSeries>> callback);
}