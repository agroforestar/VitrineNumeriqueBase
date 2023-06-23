using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ARLocation;
using Hessburg;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

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
    
    private ARAnchorManager anchorManager;
    private ARPlaneManager planeManager;

    public VolumeProfile volume;
    ColorAdjustments colorAdjustments;

    private void Start()
    {
        volume.TryGet(out colorAdjustments);
        _sun = GameObject.Find("SL_SceneLight");
        
        anchorManager = FindObjectOfType<ARAnchorManager>();
        planeManager = FindObjectOfType<ARPlaneManager>();
        
        //Pour La croissance
        sliderGrow.onValueChanged.AddListener((value) => { 
            foreach (var plant in _plants)
            {
                plant.Grow((int)value);
            }});
        
        //Pour les saisons
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

    IEnumerator InstantiateParcelle()
    {
        print($"Instantiate At {_pLat}, {_pLong}");
        
        var loc = new Location()
        {
            Latitude = _pLat,
            Longitude = _pLong,
            Altitude = -12,
            AltitudeMode = AltitudeMode.GroundRelative
        };
        
        var opts = new PlaceAtLocation.PlaceAtOptions()
        {
            HideObjectUntilItIsPlaced = true,
            MaxNumberOfLocationUpdates = 2,
            MovementSmoothing = 0.05f,
            UseMovingAverage = false,
        };
        
        float lastDistance = Single.PositiveInfinity;
        ARPlane plane = null;

        do
        {
            foreach (var ArPlane in planeManager.trackables)
            {
                var distance = Vector3.Distance(ArPlane.transform.position, loc.ToVector3());
                if (distance < lastDistance)
                    plane = ArPlane;
            }

            yield return new WaitForEndOfFrame();
        } while (plane == null);
        
        print("Not Null");
        
        ARAnchor anchor = anchorManager.AttachAnchor(plane, new Pose(plane.transform.position, plane.transform.rotation));
        var newParcelle = Instantiate(_parcelles[systemChoose], anchor.transform);
        anchorManager.anchorPrefab = newParcelle;
        
        PlaceAtLocation.AddPlaceAtComponent(newParcelle, loc, opts); //Place la parcelle sur les coordonnées
    
        GameObject[] plants = GameObject.FindGameObjectsWithTag("Plant");
        
        foreach (var plant in plants)
        {
            var index = int.Parse(plant.name.Split(" ")[0])/12;
            _plants.Add(new Plant(index, 0, 0, plant));
        }
        
        GameObject.Find("Advice").SetActive(false);
        
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

        Input.location.Start(); //Démarre le service de géolocalisation intégré à Unity

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
        
        if(systemChoose != 3)
            StartCoroutine(InstantiateParcelle()); // créé la parcelle aux coordonnées de l'utilisateur
    }

    private void Update()
    {
        if (_sunLight == null)
            return;
        
        float shadowStrengh;
        float postExposure;
        float contrast;
        
        StarsTransformParent.eulerAngles=new Vector3(_sunLight.GetStarDomeDeclination(), 0.0f, 0.0f);
        StarsTransform.localEulerAngles=new Vector3(0.0f, 360.0f/24.0f*_sunLight.timeInHours, 0.0f);


        
        Color C;
        if(_sunLight.GetSunAltitude()>180.0)
        {
            postExposure = -4f;
            shadowStrengh = 0f;
            contrast = 30f;
            C = new Color(1.0f, 1.0f, 1.0f, Mathf.Clamp((355.0f-_sunLight.GetSunAltitude())*0.05f, 0.0f, 1.0f));
        }	
        else
        {
            postExposure = 0;
            contrast = 0;
            shadowStrengh = 0.2f;
            C = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        }

        _sun.GetComponent<Light>().shadowStrength =
            Mathf.Lerp(_sun.GetComponent<Light>().shadowStrength, shadowStrengh, Time.deltaTime * 2f);
        colorAdjustments.postExposure.value = Mathf.Lerp(colorAdjustments.postExposure.value, postExposure, Time.deltaTime * 2f);
        colorAdjustments.contrast.value = Mathf.Lerp(colorAdjustments.contrast.value, contrast, Time.deltaTime * 2f);
        
        StarsMaterial.SetColor("_TintColor", C); 
    }
    
    public void BackToMenu() => SceneManager.LoadScene("Scenes/Menu");

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
