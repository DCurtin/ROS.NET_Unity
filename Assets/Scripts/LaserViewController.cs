﻿using UnityEngine;
using System.Collections;
using Messages.sensor_msgs;
using Ros_CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

public class LaserViewController : SensorTFInterface
{
    //Ros stuff
    private NodeHandle nh = null;
    private Subscriber<LaserScan> subscriber;

    //various collections 
    SortedList<DateTime, LaserScan> toDraw = new SortedList<DateTime, LaserScan>();
    List<GameObject> recycle = new List<GameObject>();
    List<GameObject> active = new List<GameObject>();

    private Messages.std_msgs.Time lastStamp = null; //used to check for out of date msgs
    private GameObject points; //will become child(0), used for cloning

    public float pointSize = 1;
    public float Decay_Time = 0f;
    //curently not in use
    //private uint maxRecycle = 100;

    void Start()
    {
        rosmanager.StartROS(this,() => {
            nh = new NodeHandle();
            subscriber = nh.subscribe<LaserScan>(topic, 1, scancb);
        });

        points = transform.GetChild(0).gameObject;
        points.hideFlags |= HideFlags.HideAndDontSave;
        points.SetActive(false);
        points.name = "Points";
       
    }

    private void scancb(LaserScan argument)
    {
   
        //toDraw.Add(argument.header.seq, argument);
        if(TFName == null || !TFName.Equals(argument.header.frame_id))
        {
            TFName = argument.header.frame_id;
        }

        if(lastStamp != null && ROS.GetTime(argument.header.stamp) < ROS.GetTime(lastStamp)) 
        {
            UnityEngine.Debug.LogError("TIME IS MOVING BACKWARDS");
        }
        lastStamp = argument.header.stamp;

        lock (toDraw)
        {
            toDraw.Add(ROS.GetTime(argument.header.stamp), argument);
        }

        
     
       

    }

    //TODO keep toDraw count at bay when decay times are low,
    //Manage Recycle count when decay time is switched from a high state
    //to a low state

    // Update is called once per frame
    void Update()
    {
        if (Decay_Time < 0.0001f)
        {

            lock (toDraw)
                while (toDraw.Count() > 1)
                {
                    //drop off extra toDraws while decay time is 0
                    remOldestFromToDraw();
                }

            lock(active)
                while(active.Count() > 1)
                {
                    //decay has been set to 0, clear active list leaving just 1
                    active.First().GetComponent<LaserScanView>().recycle();
                }
        }

        lock(toDraw)
        {

            while (toDraw.Count() > 0)
            {
                GameObject newone = null;
                bool need_a_new_one = true;

                lock (recycle)
                    if (recycle.Count() > 0)
                    {
                        need_a_new_one = false;
                        newone = popRecycle();
                    }

                if (need_a_new_one)
                {
                    newone = Instantiate(points.transform).gameObject;
                    newone.transform.SetParent(null, false);

                    //newone.hideFlags |= HideFlags.HideAndDontSave;

                    newone.GetComponent<LaserScanView>().Recylce += (oldScan) =>
                    {
                        lock (active)
                        {
                            active.Remove(oldScan);
                        }
                        lock (recycle)
                        {
                            recycle.Add(oldScan);
                        }
                    };
                    /*
                        currently not in use, may be implemented later to handle
                        cleaning up recycled GO's when the recycle list is overly 
                        large.
                        Consider checking how frequently a GO has been used based
                        on decay time.
                    */
                    /*
                    newone.GetComponent<LaserScanView>().IDied += (deadScan) =>
                    {

                        remFromRecycle(deadScan);
                        deadScan.transform.SetParent(null); //disconnect from parent
                        Destroy(deadScan); //destroy object
                    };
                    */
                }

                KeyValuePair<DateTime, LaserScan>? oldest = remOldestFromToDraw();
                newone.GetComponent<LaserScanView>().SetScan(Time.fixedTime, oldest.Value.Value, gameObject, TF);

                active.Add(newone);

            }
        }
    }

    /**
        Recycle and ToDraw interface(s) for adding and removing elements safely
    **/

    #region ToDraw interface

    KeyValuePair<DateTime, LaserScan>? remOldestFromToDraw()
    {
        if (toDraw.Count == 0) return null;
        var min = toDraw.Keys.Min();
        var kvp = new KeyValuePair<DateTime, LaserScan>(min, toDraw[min]);
        toDraw.Remove(min);
        return new Nullable<KeyValuePair<DateTime, LaserScan>>(kvp);
    }

    #endregion

    #region Recycle interface

    GameObject popRecycle()
    {
        GameObject gameObjOut;
        gameObjOut = recycle.FirstOrDefault().gameObject;
        recycle.RemoveAt(0);
        return gameObjOut;
    }

    #endregion

  
}