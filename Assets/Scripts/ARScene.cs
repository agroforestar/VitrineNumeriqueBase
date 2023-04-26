using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ARLocation;
using Hessburg;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class ARScene : MonoBehaviour
{
    private int systemChoose = Menu.systemChoose;
    private static GameObject[,,] models;

    private readonly List<Plant> _plants = new List<Plant>();
    private List<GameObject> plantsInRes;
    
    [SerializeField] private Slider sliderGrow;
    [SerializeField] private Slider sliderSeason;
    [SerializeField] private GameObject[] _parcelles = new GameObject[3];

    private SunLight _sunLight;
    private GameObject _sun;         
    
    private float _pLong;
    private float _pLat;
    private float _pAlt;
    
   public GameObject stars;

    private Material StarsMaterial;
    private Transform StarsTransform;
    private Transform StarsTransformParent;

    private void Start()
    {
        _sun = GameObject.Find("SL_SceneLight");
        
        sliderGrow.onValueChanged.AddListener((value) => {
            foreach (var plant in _plants)
            {
                plant.Grow((int)value);
            }});
        sliderSeason.onValueChanged.AddListener((value) => {
            foreach (var plant in _plants)
            {
                plant.ChangeSeason((int)value);
            }});
        
        plantsInRes = Resources.LoadAll<GameObject>($"Models").ToList();
        plantsInRes = plantsInRes.OrderBy(x => int.Parse(x.name)).ToList();
        int nbPlants = plantsInRes.Count / 12;
        models = new GameObject[nbPlants, 4, 3];
        
        print($"Nombre de plantes disponibles => {nbPlants}");
        
        for (int i = 0; i < nbPlants*12; i+=12)
            for (int j = 0; j < 12; j += 3)
                for (int k = 0; k < 3; k++)
                    models[i / 12, j / 3, k] = plantsInRes[i + j + k];

        InitSun();

        StartCoroutine(GetPosition());
    }

    void InitSun()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        
        _sunLight = GameObject.Find("SunLight").GetComponent<SunLight>();
        _sunLight.longitude = _pLong;
        _sunLight.latitude = _pLat;
        _sunLight.timeProgressFactor = 2880.0f;
        _sunLight.progressTime = true;
        
        StarsTransform=stars.transform;
        StarsTransformParent=StarsTransform.parent;
        StarsMaterial=stars.GetComponent<Renderer>().material;
    }

    private void InstantiateParcelle()
    {
        print($"Instantiate At {_pLat}, {_pLong}");
        
        var loc = new Location()
        {
            Latitude = _pLat,
            Longitude = _pLong,
            Altitude = -2,
            AltitudeMode = AltitudeMode.GroundRelative
        };
        
        var opts = new PlaceAtLocation.PlaceAtOptions()
        {
            HideObjectUntilItIsPlaced = true,
            MaxNumberOfLocationUpdates = 2,
            MovementSmoothing = 0.05f,
            UseMovingAverage = false,
        };

        var newParcelle = Instantiate(_parcelles[systemChoose]);
        
        PlaceAtLocation.AddPlaceAtComponent(newParcelle, loc, opts);

        GameObject[] plants = GameObject.FindGameObjectsWithTag("Plant");
        
        foreach (var plant in plants)
        {
            var index = int.Parse(plant.name.Split(" ")[0])/12;
            print(index);
            _plants.Add(new Plant(index, 0, 0, plant));
        }
        print("PARCELLE INSTANTIATE");
        
        
    }

    IEnumerator GetPosition()
    {
        if(!Input.location.isEnabledByUser)
        {
            print("location not enabled => permission request");
            Permission.RequestUserPermission(Permission.FineLocation);
        }
        
        if(!Input.location.isEnabledByUser)
            yield break;

        Input.location.Start(); 

        // Waits until the location service initializes
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        // If the service didn't initialize in 20 seconds this cancels location service use.
        if (maxWait < 1)
        {
            print("Timed out");
            yield break;
        }

        // If the connection failed this cancels location service use.
        if (Input.location.status == LocationServiceStatus.Failed)
        {
            print("Unable to determine device location");
            yield break;
        }

        _pLat = Input.location.lastData.latitude;
        _pLong = Input.location.lastData.longitude;
        _pAlt = Input.location.lastData.altitude;
        
        print("Lat : " + _pLat + " Long : " + _pLong + "Alt : " + _pAlt);
        
        InstantiateParcelle();
    }

    private void Update()
    {
        if (_sunLight == null)
            return;
        
        float lerpVal;
        
        StarsTransformParent.eulerAngles=new Vector3(_sunLight.GetStarDomeDeclination(), 0.0f, 0.0f);
        StarsTransform.localEulerAngles=new Vector3(0.0f, 360.0f/24.0f*_sunLight.timeInHours, 0.0f);
        
        Color C;
        if(_sunLight.GetSunAltitude()>180.0)
        {
            lerpVal = 0f;
            C = new Color(1.0f, 1.0f, 1.0f, Mathf.Clamp((355.0f-_sunLight.GetSunAltitude())*0.05f, 0.0f, 1.0f));
        }	
        else
        {
            lerpVal = 0.2f;
            C = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        }

        _sun.GetComponent<Light>().shadowStrength =
            Mathf.Lerp(_sun.GetComponent<Light>().shadowStrength, lerpVal, Time.deltaTime * 2f);

        StarsMaterial.SetColor("_TintColor", C); 
    }

    public class Plant
    {
        public GameObject go { get; set; }
        
        private Position modelsPos;

        public Plant(int x, int y, int z, GameObject instantiateGo)
        {
            modelsPos = new Position { x = x, y = y, z = z};
            go = instantiateGo;
        }
        
        public class Position
        {
            public int x { get; set; }
            public int y { get; set; }
            public int z { get; set; }
        }
        
        public void Grow(int value)
        {
            modelsPos.z = value;
            ActualiseModel();
        }

        public void ChangeSeason(int value)
        {
            modelsPos.y = value;
            ActualiseModel();
        }

        private void ActualiseModel()
        {
            var oldPlant = go;
            
            var newGo = Instantiate(models[modelsPos.x, modelsPos.y, modelsPos.z], oldPlant.transform.parent, true);
            newGo.transform.position = oldPlant.transform.position;
            go = newGo;
            Destroy(oldPlant);
        }
    }
}
