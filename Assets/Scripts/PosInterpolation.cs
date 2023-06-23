using ARLocation;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Android;

public class PosInterpolation : MonoBehaviour
{

    private List<Dictionary<string, float>> _positions = new();

    [SerializeField]
    private GameObject wall;
    [SerializeField]
    private GameObject wall2;
    // Start is called before the first frame update
    void S.tart()
    {
        
    }

    public async void SetPosition()
    {
        var position = await GetGpsPosition();
        _positions.Add(position);
                
    }

    private void Interpol()
    {
        print("Interpolation");

        var vectorsList = new List<Vector3>();

        foreach (var position in _positions)
        {
            var loc = new Location()
            {
                Latitude = position["lat"],
                Longitude = position["long"],
                Altitude = -12,
                AltitudeMode = AltitudeMode.GroundRelative
            };
            var loc2 = new Location()
            {
                Latitude = 100,
                Longitude = 100,
                Altitude = -12,
                AltitudeMode = AltitudeMode.GroundRelative
            };
            var opts = new PlaceAtLocation.PlaceAtOptions()
            {
                HideObjectUntilItIsPlaced = true,
                MaxNumberOfLocationUpdates = 2,
                MovementSmoothing = 0.1f,
                UseMovingAverage = false,
            };
            
            print($"{position["lat"]}, {position["long"]}");
            var newWall = PlaceAtLocation.CreatePlacedInstance(wall, loc, opts);
            var newWall2 = PlaceAtLocation.CreatePlacedInstance(wall2, loc2, opts);
            print(newWall.transform.position);
            print(newWall2.transform.position);
            
            vectorsList.Add(newWall.transform.position);
        }

        var sizeWall = wall.transform.localScale.x;

        var iteration = (int)(Vector3.Distance(vectorsList[0], vectorsList[1]) / sizeWall);
        
        print($"{iteration}, {sizeWall}, {vectorsList[0]}, {vectorsList[1]}");

        
        for (int i = 0; i < iteration; i++)
        {
        }


    }
    
    void Update()
    {
        if (_positions.Count >= 2)
        {
            Interpol();
            _positions.Clear();
        }
    }
    
    private async Task<Dictionary<string, float>> GetGpsPosition()
    {
        var dict = new Dictionary<string, float>()
        {
            {
                "lat", 0
            },
            {
                "long", 0
            },
            {
                "alt", 0
            }
        };
        
        if(!Input.location.isEnabledByUser)
        {
            print("location not enabled => permission request");
            Permission.RequestUserPermission(Permission.FineLocation);
        }
        
        if(!Input.location.isEnabledByUser)
            return dict;
        
        
        if(Input.location.status != LocationServiceStatus.Running)
            Input.location.Start(); 

        // Waits until the location service initializes
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            await Task.Delay(1000);
            maxWait--;
        }

        // If the service didn't initialize in 20 seconds this cancels location service use.
        if (maxWait < 1)
        {
            print("Timed out");
            return dict;
        }

        // If the connection failed this cancels location service use.
        if (Input.location.status == LocationServiceStatus.Failed)
        {
            print("Unable to determine device location");
            return dict;
        }
        
        dict["lat"] = Input.location.lastData.latitude;
        dict["long"] = Input.location.lastData.longitude;
        dict["alt"] = Input.location.lastData.altitude;
        
        print($"{dict["lat"]}, {dict["long"]}, {dict["alt"]}");


        return dict;
    }
}
