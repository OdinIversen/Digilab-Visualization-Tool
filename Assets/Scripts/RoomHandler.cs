using Speckle.ConnectorUnity.Wrappers;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public class RoomHandler : MonoBehaviour
{
    public List<GameObject> buildingList;
    public Slider transparencySlider;
    public SensorAPIBase apiHandler;
    [SerializeField] private List<Sensor> sensorData;
    public List<RoomSensorPair> roomSensorPairs;
    public Gradient colorGradient;
    public LoadTimeSeries loadTimeSeries;

    public string targetVariableName = "internalTemperature";
    public TMP_Dropdown variableDropdown;
    public HashSet<string> propertyNames = new HashSet<string>();

    [System.NonSerialized]
    public List<GameObject> roomList = new List<GameObject>();

    public Dictionary<string, Gradient> gradients;

    private const int TransparentRenderQueue = 3000;
    private const int OpaqueRenderQueue = 2000;

    public object minValue = 15;
    public object maxValue = 30;

    public Image colorScaleImage;
    public TMP_Text minText;
    public TMP_Text maxText;
    [SerializeField] private int textureWidth = 256;
    [SerializeField] private int textureHeight = 32;

    public bool stopCoroutineFlag = false;

    private bool isDropdownInitialized = false;


    void Start()
    {
        PreprocessRooms();
        SetAllRenderersToStandardShader(buildingList);
        UpdateTransparency(buildingList, transparencySlider);
        transparencySlider.onValueChanged.AddListener(delegate { UpdateTransparency(buildingList, transparencySlider); });

        InitializeGradients();
        UpdateColorRange();
        StartCoroutine(UpdateRoomColors());

        UpdateColorScaleDisplay();

        apiHandler.OnSensorDataUpdated += HandleSensorDataUpdated;
        StartCoroutine(DelayedInitialization());
    }

    IEnumerator DelayedInitialization()
    {
        yield return new WaitUntil(() => sensorData != null && sensorData.Count > 0);
        InitializeVariableDropdownWithSensorData();
    }

    private void HandleSensorDataUpdated(List<Sensor> updatedData)
    {
        sensorData = updatedData;
        
        if (!isDropdownInitialized)
        {
            InitializeVariableDropdownWithSensorData();
        }
    }

    private void InitializeVariableDropdownWithSensorData()
    {
        if (sensorData != null && sensorData.Count > 0)
        {
            foreach (StringObjectPair property in sensorData[0].data)
            {
                propertyNames.Add(property.Key);
            }

            UpdateVariableDropdown();
            
            isDropdownInitialized = true;
        }
        else
        {
            Debug.LogError("No sensor data available to populate dropdown.");
        }
    }

    private void UpdateVariableDropdown()
    {
        variableDropdown.ClearOptions();
        variableDropdown.AddOptions(propertyNames.ToList());
        variableDropdown.onValueChanged.AddListener(OnVariableDropdownValueChanged);
    }

    public void OnVariableDropdownValueChanged(int index)
    {
        ChangeTargetVariable(propertyNames.ElementAt(index));
        if (loadTimeSeries.timeLineCanvasInstance != null)
        {
            loadTimeSeries.DestroyMenu(ref loadTimeSeries.timeLineCanvasInstance);
            loadTimeSeries.PlayTimeSeries(loadTimeSeries.timeSeriesData);
        }
    }

    void PreprocessRooms()
    {
        foreach (GameObject building in buildingList)
        {
            Transform rooms = building.transform.Find("@Rooms");
            if (rooms != null)
            {
                var objectsToDestroy = rooms.Cast<Transform>().Where(child => child.GetComponent<SpeckleProperties>() == null);

                foreach (Transform room in rooms)
                {
                    SpeckleProperties speckleProperties = room.GetComponent<SpeckleProperties>();
                    if (speckleProperties != null)
                    {
                        if (speckleProperties.Data.ContainsKey("number"))
                        {
                            room.name = speckleProperties.Data["number"].ToString();
                        }

                        if (roomSensorPairs.Any(pair => pair.roomName == room.name))
                        {
                            roomList.Add(room.gameObject);
                        }
                        else
                        {
                            Destroy(room.GetComponent<MeshRenderer>());
                        }
                    }
                }

                foreach (Transform room in objectsToDestroy)
                {
                    Destroy(room.gameObject);
                }
            }
        }
    }

    void UpdateTransparency(List<GameObject> buildingList, Slider transparencySlider)
    {
        float alpha = transparencySlider.value;

        foreach (GameObject building in buildingList)
        {
            TraverseTransformTree(building.transform, current =>
            {
                // Skip if current object is a room
                if (current.name == "@Rooms" || roomSensorPairs.Any(pair => pair.roomName == current.name))
                {
                    return;
                }

                Renderer renderer = current.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if(alpha == 0)
                    {
                        renderer.enabled = false;
                    }
                    else
                    {
                        renderer.enabled = true;

                        Material[] materials = renderer.materials;
                        foreach (Material material in materials)
                        {
                            UpdateAlphaForMaterial(material, alpha);
                        }
                        renderer.materials = materials;
                    }
                }
            });
        }
    }

    void UpdateAlphaForMaterial(Material material, float alpha)
    {
        Color color = material.color;
        if(material.name.Contains("Glass"))
        {
            color.a = Mathf.Min(alpha, 0.3f);
        }
        else
        {
            color.a = alpha;
            if(alpha < 0.3f)
            {
                SetMaterialBlendMode(material, 2, UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, 0, TransparentRenderQueue);
            }
            else
            {
                SetMaterialBlendMode(material, 0, UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.Zero, 1, OpaqueRenderQueue);
            }
        }
        material.color = color;
    }

    void SetAllRenderersToStandardShader(List<GameObject> buildingList)
    {
        foreach (GameObject building in buildingList)
        {
            TraverseTransformTree(building.transform, current =>
            {
                Renderer renderer = current.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material[] materials = renderer.materials;
                    foreach (Material material in materials)
                    {
                        material.shader = Shader.Find("Standard");
                        if(material.name.Contains("Glass"))
                        {
                            SetMaterialBlendMode(material, 2, UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, 0, TransparentRenderQueue);
                        }
                    }
                    renderer.materials = materials;

                    renderer.UpdateGIMaterials();
                }
            });
        }
    }

    void SetMaterialBlendMode(Material material, float mode, UnityEngine.Rendering.BlendMode srcBlend, UnityEngine.Rendering.BlendMode dstBlend, int zWrite, int renderQueue)
    {
        material.SetFloat("_Mode", mode);
        material.SetInt("_SrcBlend", (int)srcBlend);
        material.SetInt("_DstBlend", (int)dstBlend);
        material.SetInt("_ZWrite", zWrite);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        if(mode == 2)
        {
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        else
        {
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        material.renderQueue = renderQueue;
    }

    void TraverseTransformTree(Transform root, System.Action<Transform> action)
    {
        Stack<Transform> stack = new Stack<Transform>();
        stack.Push(root);

        while(stack.Count > 0)
        {
            Transform current = stack.Pop();
            action(current);

            foreach (Transform child in current)
            {
                stack.Push(child);
            }
        }
    }

    private void InitializeGradients()
    {
        gradients = new Dictionary<string, Gradient>();

        Gradient illuminance = new Gradient();
        GradientColorKey[] illuminanceColorKeys = new GradientColorKey[2];
        illuminanceColorKeys[0] = new GradientColorKey(Color.black, 0);
        illuminanceColorKeys[1] = new GradientColorKey(Color.yellow, 1);
        illuminance.colorKeys = illuminanceColorKeys;
        gradients["illuminance"] = illuminance;
        gradients["Lux"] = illuminance;

        Gradient temperature = new Gradient();
        GradientColorKey[] temperatureColorKeys = new GradientColorKey[3];
        temperatureColorKeys[0] = new GradientColorKey(Color.blue, 0);
        temperatureColorKeys[1] = new GradientColorKey(Color.green, 0.5f);
        temperatureColorKeys[2] = new GradientColorKey(Color.red, 1);
        temperature.colorKeys = temperatureColorKeys;
        gradients["tempOutsideDevice"] = temperature;
        gradients["tempInsideDevice"] = temperature;
        gradients["temperature"] = temperature;

        Gradient humidity = new Gradient();
        GradientColorKey[] humidityColorKeys = new GradientColorKey[2];
        humidityColorKeys[0] = new GradientColorKey(Color.red, 0);
        humidityColorKeys[1] = new GradientColorKey(Color.blue, 1);
        humidity.colorKeys = humidityColorKeys;
        gradients["humOutsideDevice"] = humidity;
        gradients["humInsideDevice"] = humidity;
        gradients["humidity"] = humidity;

        Gradient motion = new Gradient();
        GradientColorKey[] motionColorKeys = new GradientColorKey[4];
        motionColorKeys[0] = new GradientColorKey(Color.green, 0.0f);
        motionColorKeys[1] = new GradientColorKey(Color.green, 0.25f);
        motionColorKeys[2] = new GradientColorKey(Color.red, 0.75f);
        motionColorKeys[3] = new GradientColorKey(Color.red, 1.0f);
        motion.colorKeys = motionColorKeys;
        GradientAlphaKey[] motionAlphaKeys = new GradientAlphaKey[6];
        motionAlphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
        motionAlphaKeys[1] = new GradientAlphaKey(1.0f, 0.25f);
        motionAlphaKeys[2] = new GradientAlphaKey(0.0f, 0.25f);
        motionAlphaKeys[3] = new GradientAlphaKey(0.0f, 0.75f);
        motionAlphaKeys[4] = new GradientAlphaKey(1.0f, 0.75f);
        motionAlphaKeys[5] = new GradientAlphaKey(1.0f, 1.0f);
        motion.alphaKeys = motionAlphaKeys;
        gradients["pIRMotion"] = motion;
        gradients["pirmotion"] = motion;

        Gradient pressure = new Gradient();
        GradientColorKey[] pressureColorKeys = new GradientColorKey[3];
        pressureColorKeys[0] = new GradientColorKey(Color.blue, 0);
        pressureColorKeys[1] = new GradientColorKey(Color.green, 0.5f);
        pressureColorKeys[2] = new GradientColorKey(Color.red, 1);
        pressure.colorKeys = pressureColorKeys;
        gradients["airPressure"] = pressure;
        gradients["noise"] = pressure;
    }

    public void ChangeTargetVariable(string variableName)
    {
        targetVariableName = variableName;
        UpdateColorRange();

        UpdateColorScaleDisplay();
    }

    public void UpdateColorRange()
    {
        switch (targetVariableName)
        {
            case "illuminance":
            case "Lux":
                minValue = 0f;
                maxValue = 3000f;
                break;
            case "externalHumidity":
            case "internalHumidity":
            case "humidity":
                minValue = 0f;
                maxValue = 100f;
                break;
            case "pressure":
                minValue = 950f;
                maxValue = 1050f;
                break;
            case "externalTemperature":
            case "internalTemperature":
            case "temperature":
                minValue = 15f;
                maxValue = 35f;
                break;
            case "noise":
                minValue = 100f;
                maxValue = 200f;
                break;
            default:
                minValue = false;
                maxValue = true;
                break;
        }

        if (gradients.ContainsKey(targetVariableName))
        {
            colorGradient = gradients[targetVariableName];
        }
        else
        {
            Debug.LogWarning($"Gradient not found for variable '{targetVariableName}'.");
        }
    }

    public IEnumerator UpdateRoomColors()
    {
        while (true)
        {
            while (stopCoroutineFlag)
            {
                yield return new WaitForSeconds(1f);
            }

            List<Sensor> sensorData = null;
            yield return apiHandler.GetSensorDataCoroutine((data) => sensorData = data);

            if (sensorData != null)
            {
                foreach (RoomSensorPair pair in roomSensorPairs)
                {
                    Sensor matchingSensor = sensorData.FirstOrDefault(s => s.name == pair.sensorName);
                    
                    if (matchingSensor != null)
                    {
                        foreach (StringObjectPair property in matchingSensor.data)
                        {
                            propertyNames.Add(property.Key);

                            if (property.Key == targetVariableName)
                            {
                                object value = property.Value;
                                Color newColor = colorGradient.Evaluate(NormalizeValue(value));
                                newColor.a = 1;
                                if (roomList.Any(room => room.name == pair.roomName))
                                {
                                    GameObject room = roomList.Find(room => room.name == pair.roomName);
                                    room.GetComponent<Renderer>().material.color = newColor;
                                }
                            }
                        }
                    }
                }
            }
        }
    }



    public float NormalizeValue(object value)
    {
        if (value is bool)
        {
            return (bool)value ? 1f : 0f;
        }
        else if (value is float)
        {
            float floatValue = (float)value;
            float min = minValue as float? ?? float.MinValue;
            float max = maxValue as float? ?? float.MaxValue;

            return Mathf.Clamp01((floatValue - min) / (max - min));
        }
        else
        {
            return 0f;
        }
    }

    private Texture2D GenerateGradientTexture(Gradient gradient, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height);

        for (int i = 0; i < width; i++)
        {
            Color color = gradient.Evaluate((float)i / width);
            for (int j = 0; j < height; j++)
            {
                texture.SetPixel(i, j, color);
            }
        }

        texture.Apply();
        return texture;
    }

    private void UpdateColorScaleDisplay()
    {
        UpdateGradientTexture();
        UpdateMinMaxText();
    }

    private void UpdateGradientTexture()
    {
        Texture2D gradientTexture = GenerateGradientTexture(colorGradient, textureWidth, textureHeight);
        colorScaleImage.sprite = Sprite.Create(gradientTexture, new Rect(0, 0, textureWidth, textureHeight), new Vector2(0.5f, 0.5f));
    }

    private void UpdateMinMaxText()
    {
        minText.text = minValue.ToString();
        maxText.text = maxValue.ToString();
    }

    [System.Serializable]
    public class RoomSensorPair
    {
        public string roomName;
        public string sensorName;
    }
}