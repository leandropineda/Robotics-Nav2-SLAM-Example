using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Unity.Robotics.Core;
using Unity.Robotics.ROSTCPConnector;


public class Cam : MonoBehaviour
{
    private Camera _camera;
    public string FrameId = "Camera";
    public int resolutionWidth = 1920;
    public int resolutionHeight = 1080;
    public string Encoding = "rgb8";
    public byte Is_bigendian = 0;
    public uint Step = 3*1920;

    private RosMessageTypes.Sensor.ImageMsg message;
    private Texture2D texture2D;
    private Rect rect;

    [SerializeField]
    [Header("Variables")] public string topic = "main_camera/image_raw";

    ROSConnection m_Ros;

    protected virtual void Start()
    {
        m_Ros = ROSConnection.instance;
        m_Ros.RegisterPublisher(topic, "sensor_msgs/Image");
        
        // Note: The camera component needs to be tagged as MainCamera
        // https://docs.unity3d.com/ScriptReference/Camera-main.html
        _camera = Camera.main;
         if (_camera)
        {
            Debug.Log("Found a main camera!");
            Debug.Log(_camera.scaledPixelHeight);
            Debug.Log(_camera.scaledPixelWidth);
        }
        InitializeGameObject();
        InitializeMessage();
        // onPostRender doesn't work for URP, so we need to use this method
        // instead to do the post render processing.
        // https://docs.unity3d.com/ScriptReference/Rendering.RenderPipelineManager-endCameraRendering.html
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (texture2D != null && camera == this._camera)
            UpdateMessage();
    }

    private void InitializeGameObject()
    {
        texture2D = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);
        rect = new Rect(0, 0, resolutionWidth, resolutionHeight);
        // NOTE (FlorGrosso): this step is redirecting camera output to a new texture. 
        // This will make the 'Game' view in Unity to dissappear (you'll get a 'No video'
        // meesage.)
        _camera.targetTexture = new RenderTexture(resolutionWidth, resolutionHeight, 24);
    }

    private void InitializeMessage()
    {
        message = new RosMessageTypes.Sensor.ImageMsg();
        message.header.frame_id = FrameId;
        message.height = Convert.ToUInt32(resolutionHeight);
        message.width = Convert.ToUInt32(resolutionWidth);
        message.encoding = Encoding;

        message.is_bigendian = Is_bigendian;
        message.step = Step;
    }

    private void UpdateMessage()
    {
        // Get the pixels data and turn it into an image message
        // Based on: https://github.com/siemens/ros-sharp/issues/389
        var timestamp = new TimeStamp(Clock.time);
        message.header = new HeaderMsg
        {
            stamp = new TimeMsg
            {
                sec = timestamp.Seconds,
                nanosec = timestamp.NanoSeconds,
            },
            frame_id = FrameId
        };

        texture2D.ReadPixels(rect, 0, 0);
        byte [] image =  texture2D.GetRawTextureData();

        // Note (FlorGrosso): GetRawTextureData returns an image that
        // is rotated 180 degrees and mirrored vertically. Here we
        // process the data to undo the rotation, but the output is
        // still mirrored.
        message.data = new byte[image.Length];
        for(int i=0; i < message.data.Length; i+=3){
            message.data[message.data.Length-i-3] = image[i];
            message.data[message.data.Length-i-2] = image[i+1];
            message.data[message.data.Length-i-1] = image[i+2];
        }

        // Note (FlorGrosso): This code rotates AND mirrors the
        // image. It makes Unity slower until it hangs though,
        // so I'm skipping it.
        // message.data = new byte[image.Length];
        // for(int i=0; i<message.data.Length; i+=resolutionWidth*3){
        //     int n = message.data.Length-resolutionWidth*3-i;
        //     for(int j=0; j<resolutionWidth; j+=3){
        //         Debug.Log(n);
        //         Debug.Log(i);
        //         Debug.Log(j);
        //         message.data[n+j] = image[i+j];
        //         message.data[n+j+1] = image[i+j+1];
        //         message.data[n+j+2] = image[i+j+2];
        //     }
        // }

        m_Ros.Send(topic, message);
    }

}