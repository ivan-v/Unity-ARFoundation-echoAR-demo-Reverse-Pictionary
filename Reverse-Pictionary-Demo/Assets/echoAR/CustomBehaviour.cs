/**********te****************************************************************
* Copyright (C) echoAR, Inc. 2018-2020.                                   *
* echoAR, Inc. proprietary and confidential.                              *
*                                                                         *
* Use subject to the terms of the Terms of Service available at           *
* https://www.echoar.xyz/terms, or another agreement                      *
* between echoAR, Inc. and you, your company or other organization.       *
***************************************************************************/
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/*
 * CustomBehavior script is run for each model in the echoAR console whenever an echoAR object is instantiated
 */
public class CustomBehaviour : MonoBehaviour
{
    [HideInInspector]
    public Entry entry;
    
    private string type = ""; //Metadata found in echoAR objects (e.g. "notepad" or "pen")
    private Rigidbody rb; //Rigidbody is attached to the car/pen so that it is affected by gravity

    //Defines the linear and angular speed of the car/pen
    private float movementSpeed = 0.5f;
    private float rotationalSpeed = 90.0f;

    public float verticalInput; //Specifies the forward/backward direction to move in (-1/0/1)
    public float horizontalInput; //Specifies the left/right direction to move in (-1/0/1)

    public int previousTime; //Keeps track of the previous second that was displayed
    public int timeDisplay; //Keeps track of the current time displayed

    public System.Random rnd = new System.Random(); //Creates a random seed

    public string currentWord = "";
    public string[] drawingGoals = new string[]
    { 
        "boat", "person", "tree", "house", "heart", "bear", "guitar", "robot", "flower", "dog", "duck" 
    };

    GameObject statusText;
    TextMesh statusTextMesh;

    /*
     * Attaches a MeshCollider to object and its children (recursive)
     * GameObject toAttach: the object that will receive the MeshCollider
     * bool convex: whether or not the MeshCollider should be convex
     */
    void recursivelyCollide(GameObject toAttach, bool convex)
    {
        if (toAttach.GetComponent<MeshCollider>() == null)
        {
            toAttach.AddComponent<MeshCollider>();
            toAttach.GetComponent<MeshCollider>().convex = convex;
        }

        //Do the same to its children (recursive)
        foreach (Transform t in toAttach.transform)
        {
            recursivelyCollide(t.gameObject, convex);
        }
        GameObject carMesh = GameObject.Find("node-0");
        carMesh.GetComponent<MeshRenderer>().enabled = false;
    }
    

    void Start()
    {
        // Add RemoteTransformations script to object and set its entry
        this.gameObject.AddComponent<RemoteTransformations>().entry = entry;
        entry.getAdditionalData().TryGetValue("type", out type); //Read the "type" metadata

        previousTime = (int)Time.time;
        timeDisplay = 19;

        if(type == "pen")
        {

            // Initialize the display text
            statusText = new GameObject();
            statusText.name = "status";
            statusText.transform.position = new Vector3(1, 3, 8.5f);
            statusTextMesh = statusText.AddComponent<TextMesh>();
            currentWord = drawingGoals[rnd.Next(0, drawingGoals.Length)]; //Choose a random word from list
            statusTextMesh.text = "Draw a " + currentWord + "!\n" + timeDisplay.ToString();
            statusTextMesh.fontSize = 20;
            statusTextMesh.anchor = TextAnchor.MiddleCenter;
            statusTextMesh.alignment = TextAlignment.Center;
            /*
             * Adds a dummy parent between this.gameObject and echoAR root object
             * i.e. 
             *         [echoAR]
             *            /
             *       [parent]
             *          /
             *  [this.gameObject]
             *    
             * It is difficult to apply transformations directly to the 3D model since the position/rotation are controlled by the echoAR console
             * We can easily apply transformations to the 3D model by applying the transformations to the dummy parent instead
             */
            GameObject parent = new GameObject();
            parent.transform.parent = this.gameObject.transform.parent;
            this.gameObject.transform.parent = parent.transform;

            //Reset the 3D model's local position relative to the dummy parent
            this.gameObject.transform.localPosition = Vector3.zero;
            this.gameObject.transform.localRotation = Quaternion.identity;

            //Reset the parent's local position relative to the echoAR object
            //invokes the onClick event of the reset button
            GameObject rb = GameObject.Find("Reset Button");
            rb.GetComponent<Button>().onClick.Invoke();
            GameObject carMesh = GameObject.Find("node-0");
            carMesh.GetComponent<MeshRenderer>().enabled = false;
        }
        else
        {
            //If this.gameObject is the track, reset its local position when it is instantiated
            this.gameObject.transform.localPosition = Vector3.zero;
            this.gameObject.transform.localRotation = Quaternion.identity;
        }
    }

    
    /*
     * Called every frame
     */
    void Update()
    {
        /*
         * If type is pen:
         *     Add MeshCollider that is convex
         *     Needs to be convex because it will also have a Rigidbody
         *     Rigidbody + non-convex is no longer supported
         * Else:
         *     Add MeshCollider that is not convex so that we get all concave contours of the track
         */
        if (Time.time - previousTime >= 1)
        {
            previousTime = (int)Time.time;
            if (timeDisplay > 1)
            {
                timeDisplay -= 1;
            } else {
                timeDisplay = 19;
                //Choose new word from list
                currentWord = drawingGoals[rnd.Next(0, drawingGoals.Length)];
                //Clear the trail once time is up
                GameObject pen = GameObject.Find("Convertible.glb");
                pen.GetComponent<TrailRenderer>().Clear();
                GameObject rb = GameObject.Find("Reset Button");
                rb.GetComponent<Button>().onClick.Invoke();
                GameObject carMesh = GameObject.Find("node-0");
                carMesh.GetComponent<MeshRenderer>().enabled = false;
            }
            //Update time counter
            GameObject statusText = GameObject.Find("status");
            statusTextMesh = statusText.GetComponent<TextMesh>();
            statusTextMesh.text = "Draw a " + currentWord + "!\n" + timeDisplay.ToString();
            statusTextMesh.fontSize = 20;
            statusTextMesh.anchor = TextAnchor.MiddleCenter;
            statusTextMesh.alignment = TextAlignment.Center;
        }
        recursivelyCollide(this.gameObject, type == "pen");

        if (type == "pen")
        {

            // GameObject eAR = GameObject.Find("echoAR");
            // eAR.GetComponent<Rigidbody>().isKinematic = true;

            Transform parentTransform = transform.parent.transform;
            parentTransform.tag = "toControl"; //We want to control the parent and the ButtonController script will find all objects with this tag

            if (rb == null)
            {
                rb = gameObject.transform.parent.gameObject.AddComponent<Rigidbody>();
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; //Better for small objects that move fast
                
                TrailRenderer tr = gameObject.AddComponent<TrailRenderer>() as TrailRenderer;
                tr.material = new Material(Shader.Find("Sprites/Default"));
                tr.startColor = Color.black;
                tr.endColor = Color.black;
                tr.time = 120;
                tr.alignment = LineAlignment.TransformZ;


            }
            
            //Translation
            parentTransform.position = parentTransform.position + parentTransform.forward * verticalInput * movementSpeed * Time.deltaTime;

            //Rotation, but only on the y axis
            parentTransform.localEulerAngles = new Vector3(parentTransform.localEulerAngles.x, parentTransform.localEulerAngles.y + horizontalInput * verticalInput * rotationalSpeed * Time.deltaTime, parentTransform.localEulerAngles.z);

            //Detects when the car/pen falls of the track and invokes the onClick event of the reset button
            if(parentTransform.localPosition.y <= -10)
            {
                GameObject rb = GameObject.Find("Reset Button");
                rb.GetComponent<Button>().onClick.Invoke();
                GameObject carMesh = GameObject.Find("node-0");
                carMesh.GetComponent<MeshRenderer>().enabled = false;
            }
        }
    }
}