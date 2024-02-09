using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using TMPro;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using static RoomHandler;

public class LoadTimeSeries : MonoBehaviour
{
    public GameObject timeSeriesMenuPrefab;
    public GameObject csvMenuPrefab;
    public GameObject arduinoMenuPrefab;
    public GameObject arduinoDataLoader;
    public GameObject playTimeSeriesMenuPrefab;
    public GameObject warningMenuPrefab;
    public GameObject progressBarPrefab;
    public GameObject progressBarInstanse;

    public RoomHandler roomHandler;
    public Canvas mainCanvas;
    public GameObject timeLineCanvas;

    private GameObject timeSeriesMenuInstance;
    private GameObject csvMenuInstance;
    private GameObject arduinoMenuInstance;
    private GameObject playTimeSeriesMenuInstance;
    public GameObject timeLineCanvasInstance;
    private GameObject warningMenuInstance;

    public List<TimeSeries> timeSeriesData;
    private List<TimeSeriesTextureData> texturesData;

    private DateTime minTimestamp;
    private DateTime maxTimestamp;

    private Coroutine pixelCoroutine;
    private float selectedSpeed = 300f;
    private float selectedInterval = 300f;
    public float[] speedValues;
    public float[] intervalValues;

    private List<TimeSeriesTextureData> instantiatedTextures = new List<TimeSeriesTextureData>();

    string path;

    void Start()
    {
        path = Application.dataPath + "/TimeSeriesDataFolder/";
    }

    public void InstantiateTimeSeriesMenu()
    {
        if (timeSeriesMenuInstance != null || csvMenuInstance != null || arduinoMenuInstance != null || playTimeSeriesMenuInstance != null || timeLineCanvasInstance != null || warningMenuInstance != null)
        {
            return;
        }
        
        timeSeriesMenuInstance = Instantiate(timeSeriesMenuPrefab, transform.position, transform.rotation);

        Button[] buttons = timeSeriesMenuInstance.GetComponentsInChildren<Button>();

        foreach (Button button in buttons)
        {
            switch (button.name)
            {
                case "Button - Close":
                    button.onClick.AddListener(() => DestroyMenu(ref timeSeriesMenuInstance));
                    break;
                case "Button - CSV":
                    button.onClick.AddListener(InstantiateCSVMenu);
                    button.onClick.AddListener(() => DestroyMenu(ref timeSeriesMenuInstance));
                    break;
            }
        }
    }

    public void InstantiateCSVMenu()
    {
        csvMenuInstance = Instantiate(csvMenuPrefab, transform.position, transform.rotation);

        Button[] buttons = csvMenuInstance.GetComponentsInChildren<Button>();

        foreach (Button button in buttons)
        {
            switch (button.name)
            {
                case "Button - Close":
                    button.onClick.AddListener(() => DestroyMenu(ref csvMenuInstance));
                    break;
                case "Button - Arduino":
                    button.onClick.AddListener(InstantiateArduinoMenu);
                    button.onClick.AddListener(() => DestroyMenu(ref csvMenuInstance));
                    break;
            }
        }
    }

    public void InstantiateArduinoMenu()
    {
        arduinoMenuInstance = Instantiate(arduinoMenuPrefab, transform.position, transform.rotation);

        Button[] buttons = arduinoMenuInstance.GetComponentsInChildren<Button>();

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        SetDropdownOptions();

        TMP_Text pathText = arduinoMenuInstance.GetComponentsInChildren<TMP_Text>()[1];
        pathText.text = path;

        foreach (Button button in buttons)
        {
            switch (button.name)
            {
                case "Button - Close":
                    button.onClick.AddListener(() => DestroyMenu(ref arduinoMenuInstance));
                    break;
                case "Button - Refresh":
                    button.onClick.AddListener(SetDropdownOptions);
                    break;
                case "Button - Load":
                    button.onClick.AddListener(() => 
                    {
                        TMP_Dropdown dropdown = arduinoMenuInstance.GetComponentsInChildren<TMP_Dropdown>()[0];
                        string selectedOption = dropdown.options[dropdown.value].text;
                        string fullPath = Path.Combine(Application.dataPath + "/TimeSeriesDataFolder/", selectedOption);
                        
                        ArduinoDataLoader loader = arduinoDataLoader.GetComponent<ArduinoDataLoader>();
                        loader.LoadTimeSeriesFromFolder(fullPath, timeSeriesData => {

                            if (timeSeriesData != null)
                            {
                                InstantiatePlayTimeSeriesMenu(timeSeriesData);
                            }
                            else
                            {
                                InstantiateWarningMenu();
                            }
                            
                            DestroyMenu(ref arduinoMenuInstance);
                        });
                    });
                    break;
            }
        }
    }

    public void InstantiateWarningMenu()
    {
        GameObject warningMenuInstance = Instantiate(warningMenuPrefab, transform.position, transform.rotation);
        
        Button[] buttons = warningMenuInstance.GetComponentsInChildren<Button>();
        
        foreach (Button button in buttons)
        {
            switch (button.name)
            {
                case "Button - Close":
                    button.onClick.AddListener(() => DestroyMenu(ref warningMenuInstance));
                    break;
            }
        }
    }

    public void InstantiatePlayTimeSeriesMenu(List<TimeSeries> timeSeriesData)
    {
        playTimeSeriesMenuInstance = Instantiate(playTimeSeriesMenuPrefab, transform.position, transform.rotation);

        Button[] buttons = playTimeSeriesMenuInstance.GetComponentsInChildren<Button>();
        TMP_Text[] texts = playTimeSeriesMenuInstance.GetComponentsInChildren<TMP_Text>();

        foreach (Button button in buttons)
        {
            switch (button.name)
            {
                case "Button - Close":
                    button.onClick.AddListener(() => DestroyMenu(ref playTimeSeriesMenuInstance));
                    break;
                case "Button - Play":
                    button.onClick.AddListener(() => 
                    {
                        StartCoroutine(InitializeTimeLinesCoroutine(timeSeriesData, (texturesDataResult) => {
                            texturesData = texturesDataResult;
                            roomHandler.OnVariableDropdownValueChanged(roomHandler.variableDropdown.value);
                            PlayTimeSeries(timeSeriesData);
                            DestroyMenu(ref playTimeSeriesMenuInstance);
                        }));
                    });
                    break;
            }
        }

        minTimestamp = DateTime.MaxValue;
        maxTimestamp = DateTime.MinValue;
        int totalCount = 0;

        foreach (var timeSeries in timeSeriesData)
        {
            foreach (var data in timeSeries.data)
            {
                if (data.measuredAt.Any())
                {
                    DateTime currentMin = data.measuredAt.Min();
                    DateTime currentMax = data.measuredAt.Max();
                    int currentCount = data.measuredAt.Count;

                    if (currentMin < minTimestamp)
                    {
                        minTimestamp = currentMin;
                    }

                    if (currentMax > maxTimestamp)
                    {
                        maxTimestamp = currentMax;
                    }

                    totalCount += currentCount;
                }
            }
        }

        foreach (TMP_Text text in texts)
        {
            switch (text.name)
            {
                case "Text - Start Date":
                    text.text = minTimestamp == DateTime.MaxValue ? "No data" : minTimestamp.ToString();
                    break;
                case "Text - End Date":
                    text.text = maxTimestamp == DateTime.MinValue ? "No data" : maxTimestamp.ToString();
                    break;
                case "Text - Total Readings":
                    text.text = totalCount.ToString();
                    break;
                case "Text - Readings per":
                    text.text = (timeSeriesData[0].data[0].value.Count).ToString();
                    break;
            }
        }
    }

    public void InstantiateTimelineCanvas()
    {
        timeLineCanvasInstance = Instantiate(timeLineCanvas, transform.position, transform.rotation);
        instantiatedTextures = new List<TimeSeriesTextureData>();

        Button[] buttons = timeLineCanvasInstance.GetComponentsInChildren<Button>();
        TMP_Text[] texts = timeLineCanvasInstance.GetComponentsInChildren<TMP_Text>();

        foreach (Button button in buttons)
        {
            switch (button.name)
            {
                case "Button - Close":
                    button.onClick.AddListener(() => 
                    {
                        if (pixelCoroutine != null)
                            StopCoroutine(pixelCoroutine);
                        roomHandler.stopCoroutineFlag = false;
                        DestroyMenu(ref timeLineCanvasInstance);
                    });
                    break;
                case "Button - Play":
                    button.onClick.AddListener(() => 
                    {
                        if (pixelCoroutine != null)
                            StopCoroutine(pixelCoroutine);
                        pixelCoroutine = StartCoroutine(IteratePixels());
                        roomHandler.stopCoroutineFlag = true;
                    });
                    break;
                case "Button - Pause":
                    button.onClick.AddListener(() => 
                    {
                        if (pixelCoroutine != null)
                            StopCoroutine(pixelCoroutine);
                    });
                    break;
            }
        }

        foreach (TMP_Text text in texts)
        {
            switch (text.name)
            {
                case "Text - Start Date":
                    text.text = minTimestamp == DateTime.MaxValue ? "No data" : minTimestamp.ToString();
                    break;
                case "Text - End Date":
                    text.text = maxTimestamp == DateTime.MinValue ? "No data" : maxTimestamp.ToString();
                    break;
                case "Text - Current time":
                    text.text = "Not Started";
                    break;
            }
        }

        TMP_Dropdown[] dropdowns = timeLineCanvasInstance.GetComponentsInChildren<TMP_Dropdown>();

        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            switch (dropdown.name)
            {
                case "Dropdown - Speed":
                    dropdown.ClearOptions();
                    List<string> speedOptions = new List<string>(Array.ConvertAll(speedValues, x => x.ToString()));
                    dropdown.AddOptions(speedOptions);
                    dropdown.onValueChanged.AddListener(delegate { SpeedDropdownValueChanged(dropdown); });
                    break;
                case "Dropdown - Interval":
                    dropdown.ClearOptions();
                    List<string> intervalOptions = new List<string>(Array.ConvertAll(intervalValues, x => x.ToString()));
                    dropdown.AddOptions(intervalOptions);
                    dropdown.onValueChanged.AddListener(delegate { IntervalDropdownValueChanged(dropdown); });
                    break;
            }
        }
    }

    void SpeedDropdownValueChanged(TMP_Dropdown dropdown)
    {
        int selectedIndex = dropdown.value;
        selectedSpeed = speedValues[selectedIndex];
    }

    void IntervalDropdownValueChanged(TMP_Dropdown dropdown)
    {
        int selectedIndex = dropdown.value;
        selectedInterval = intervalValues[selectedIndex];
    }

    public void InstantiateProgressBar(string process)
    {
        progressBarInstanse = Instantiate(progressBarPrefab, transform.position, transform.rotation);

        TMP_Text[] texts = progressBarInstanse.GetComponentsInChildren<TMP_Text>();

        foreach (TMP_Text text in texts)
        {
            switch (text.name)
            {
                case "Text - Process":
                    text.text = process;
                    break;
            }
        }
    }

    public void UpdateProgressBar(float value)
    {
        Slider slider = progressBarInstanse.GetComponentInChildren<Slider>();

        slider.value = value;
    }

    IEnumerator IteratePixels()
    {
        GameObject timeTop = GameObject.Find("Time - Top");
        TMP_Text currentTimeText = timeLineCanvasInstance.GetComponentsInChildren<TMP_Text>().FirstOrDefault(text => text.name == "Text - Current time");

        if (instantiatedTextures.Count == 0)
        {
            yield break;
        }

        int width = instantiatedTextures[0].Texture.width;
        int height = instantiatedTextures[0].Texture.height;

        for (int x = 0; x < width; x += Math.Max(1,(int)(selectedInterval / 32)))
        {
            for (int y = 0; y < height; y += (int)Math.Min(32, Math.Max(1, (int)Math.Floor((double)(selectedInterval % 32)))))
            {
                foreach (TimeSeriesTextureData textureData in instantiatedTextures)
                {
                    if (timeLineCanvasInstance == null)
                    {
                        yield break;
                    }
                    
                    Color color = textureData.Texture.GetPixel(x, y);
                    timeTop.transform.localPosition = new Vector3(((x / (float)width) * 1500f) - 750f, timeTop.transform.localPosition.y, timeTop.transform.localPosition.z);
                    
                    float t = (float)x / width;
                    TimeSpan duration = maxTimestamp - minTimestamp;
                    DateTime currentTime = minTimestamp + TimeSpan.FromTicks((long)(t * duration.Ticks));

                    currentTimeText.text = currentTime.ToString();

                    yield return StartCoroutine(UpdateColorsFromTimeSeries(color, textureData.SensorName));
                }

                yield return new WaitForSeconds(1f / selectedSpeed);
            }
        }
    }

    IEnumerator UpdateColorsFromTimeSeries(Color color, string sensorName)
    {
        RoomSensorPair pair = roomHandler.roomSensorPairs.FirstOrDefault(p => p.sensorName == sensorName);

        if (pair != null)
        {
            GameObject room = roomHandler.roomList.FirstOrDefault(r => r.name == pair.roomName);

            if (room != null)
            {
                Renderer renderer = room.GetComponent<Renderer>();
                if (renderer != null)
                {
                    color.a = 1;
                    renderer.material.color = color;
                }
            }
        }

        yield return null;
    }

    public void PlayTimeSeries(List<TimeSeries> timeSeriesData)
    {
        InstantiateTimelineCanvas();

        Transform panelTransform = timeLineCanvasInstance.transform.Find("Panel");

        float yPosition = 32f;

        foreach (var textureData in texturesData)
        {
            if (textureData.Key == roomHandler.targetVariableName)
            {
                GameObject newImage = new GameObject("TimeSeriesImage");
                newImage.transform.SetParent(panelTransform, false);
                newImage.transform.SetAsFirstSibling();

                RawImage rawImage = newImage.AddComponent<RawImage>();
                rawImage.texture = textureData.Texture;

                rawImage.rectTransform.sizeDelta = new Vector2(1500f, textureData.Texture.height);
                
                rawImage.rectTransform.localPosition = new Vector3(rawImage.rectTransform.localPosition.x, yPosition, rawImage.rectTransform.localPosition.z);
                
                yPosition -= textureData.Texture.height;

                instantiatedTextures.Add(textureData);
            }
        }
    }


    IEnumerator InitializeTimeLinesCoroutine(List<TimeSeries> timeSeriesData, Action<List<TimeSeriesTextureData>> callback)
    {
        List<TimeSeriesTextureData> texturesData = new List<TimeSeriesTextureData>();
        TimeSpan threshold = TimeSpan.FromSeconds(10); // Set this to a suitable value based on your data

        int totalDataCount = timeSeriesData.Sum(t => t.data.Count);
        int currentDataCount = 0;

        InstantiateProgressBar("Initializing");

        foreach (var timeSeries in timeSeriesData)
        {
            foreach (var data in timeSeries.data)
            {
                roomHandler.ChangeTargetVariable(data.key);
                int maxTextureWidth = 8192;
                int maxPixels = maxTextureWidth * 32;
                    
                Texture2D texture = new Texture2D(maxTextureWidth, 32);
                
                for (int i = 0; i < maxPixels; i++)
                {
                    texture.SetPixel(i / 32, i % 32, Color.white);
                }
                
                if (data.value.Count > 0)
                {
                    TimeSpan span = maxTimestamp - minTimestamp;

                    for (int i = 0; i < data.value.Count - 1; i++)
                    {
                        Gradient gradient = roomHandler.gradients[data.key];
                        Color currentColor = gradient.Evaluate(roomHandler.NormalizeValue(data.value[i]));
                        Color nextColor = gradient.Evaluate(roomHandler.NormalizeValue(data.value[i + 1]));

                        int pixelPosition = (int)(((data.measuredAt[i] - minTimestamp).TotalSeconds / span.TotalSeconds) * maxPixels);
                        int nextPixelPosition = (int)(((data.measuredAt[i + 1] - minTimestamp).TotalSeconds / span.TotalSeconds) * maxPixels);

                        texture.SetPixel(pixelPosition / 32, pixelPosition % 32, currentColor);

                        if (data.measuredAt[i + 1] - data.measuredAt[i] > threshold)
                        {
                            for (int j = pixelPosition + 1; j < nextPixelPosition; j++)
                            {
                                texture.SetPixel(j / 32, j % 32, Color.white);
                            }
                        }
                        else
                        {
                            for (int j = pixelPosition + 1; j < nextPixelPosition; j++)
                            {
                                float t = (float)(j - pixelPosition) / (nextPixelPosition - pixelPosition);
                                Color interpolatedColor = Color.Lerp(currentColor, nextColor, t);
                                texture.SetPixel(j / 32, j % 32, interpolatedColor);
                            }
                        }
                    }

                    Gradient lastGradient = roomHandler.gradients[data.key];
                    Color lastColor = lastGradient.Evaluate(roomHandler.NormalizeValue(data.value[^1]));
                    int lastPixelPosition = (int)(((data.measuredAt[^1] - minTimestamp).TotalSeconds / span.TotalSeconds) * maxPixels);
                    texture.SetPixel(lastPixelPosition / 32, lastPixelPosition % 32, lastColor);
                }

                texture.Apply();

                texturesData.Add(new TimeSeriesTextureData
                {
                    Texture = texture,
                    Key = data.key,
                    SensorName = timeSeries.sensorName
                });

                currentDataCount += 1;
                UpdateProgressBar((float)currentDataCount / totalDataCount);

                yield return null;
            }
        }

        DestroyMenu(ref progressBarInstanse);
        callback?.Invoke(texturesData);
    }

    public class TimeSeriesTextureData
    {
        public Texture2D Texture;
        public string Key;
        public string SensorName;
    }

    public void DestroyMenu(ref GameObject menuInstance)
    {
        if (menuInstance != null)
        {
            Destroy(menuInstance);
            menuInstance = null;
        }
    }

    private void SetDropdownOptions()
    {
        string[] directories = Directory.GetDirectories(path);

        TMP_Dropdown dropdown = arduinoMenuInstance.GetComponentsInChildren<TMP_Dropdown>()[0];
        dropdown.ClearOptions();

        List<string> options = new List<string>();

        foreach (string dir in directories)
        {
            string folderName = Path.GetFileName(dir);
            options.Add(folderName);
        }

        dropdown.AddOptions(options);
    }
}