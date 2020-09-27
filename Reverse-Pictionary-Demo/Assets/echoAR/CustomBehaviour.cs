﻿/**********te****************************************************************
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
using System.Linq;
using System;

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
    public List<string> drawingGoals = new List<string>();
    public List<int> drawingTimes = new List<int>();

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

        if(type == "pen")
        {
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
            string[] keysToBeFiltered = new string[]
            {
                "qrWebXRStorageID", "shortURL", "qrARjsStorageFilename", "qrARjsTargetStorageFilename", 
                "qrARjsMarkerStorageFilename", "qrARjsTargetStorageID", "scale", 
                "qrARjsStorageID", "qrWebARStorageID", "accessHistory", "createdAt",
                "qrWebXRStorageFilename", "x", "y", "z", "lastAccessed", "qrARjsMarkerStorageID",
                "qrWebARStorageFrame", "qrWebARStorageFilename", "type",
            };
            var myDict = entry.getAdditionalData().Where(p => !keysToBeFiltered.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
            
            string toString = "";
            foreach (string key in myDict.Keys)
            {   
                drawingGoals.Add(key);
                drawingTimes.Add(Int16.Parse(myDict[key]));
                toString += key + "=" + myDict[key] + "\n";
            }

            // Initialize the display text
            statusText = new GameObject();
            statusText.name = "status";
            statusText.transform.position = new Vector3(1, 3, 8.5f);
            statusTextMesh = statusText.AddComponent<TextMesh>();
            currentWord = drawingGoals[rnd.Next(0, drawingGoals.Count)]; //Choose a random word from list
            timeDisplay = drawingTimes[drawingGoals.IndexOf(currentWord)];
            statusTextMesh.text = "Draw a " + currentWord + "!\n" + timeDisplay.ToString();
            statusTextMesh.fontSize = 20;
            statusTextMesh.anchor = TextAnchor.MiddleCenter;
            statusTextMesh.alignment = TextAlignment.Center;


            //If this.gameObject is the notepad, reset its local position when it is instantiated
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
                //Choose new word from list
                currentWord = drawingGoals[rnd.Next(0, drawingGoals.Count)];
                timeDisplay = drawingTimes[drawingGoals.IndexOf(currentWord)];
                //Clear the trail once time is up
                GameObject pen = GameObject.Find("Ball.glb");
                pen.GetComponent<TrailRenderer>().Clear();
                GameObject rb = GameObject.Find("Reset Button");
                rb.GetComponent<Button>().onClick.Invoke();
                GameObject carMesh = GameObject.Find("node-0");
                carMesh.GetComponent<MeshRenderer>().enabled = false;
            }
            //Update time counter
            GameObject statusText = GameObject.Find("status");
            statusTextMesh = statusText.GetComponent<TextMesh>();
            Debug.Log(currentWord[0]);
            statusTextMesh.text = "Draw a " + currentWord + "!\n" + timeDisplay.ToString();
            statusTextMesh.fontSize = 20;
            statusTextMesh.anchor = TextAnchor.MiddleCenter;
            statusTextMesh.alignment = TextAlignment.Center;
        }
        recursivelyCollide(this.gameObject, type == "pen");

        if (type == "pen")
        {
    
            // In case the notepad object keeps falling through, uncomment this:
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