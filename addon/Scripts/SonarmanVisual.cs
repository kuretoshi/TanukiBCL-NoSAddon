using System;
using System.Collections.Generic;
using UnityEngine;

namespace TanukiBCL.Roles;

// Owner-only radar display and local game SE. No voice-playback routing,
// colliders or shared cosmetics are created.
public sealed class SonarmanVisual : IDisposable
{
    private const float Radius = 1.15f;
    private readonly GameObject root;
    private readonly Material material;
    private readonly Transform scan;
    private readonly LineRenderer pulse;
    private readonly AudioSource sound;
    private readonly List<Mesh> meshes = new();
    private readonly GameObject[] dots = new GameObject[24];
    private static Sprite? disc;
    public SonarmanVisual()
    {
        root = new GameObject("TanukiSonarmanRadar");
        root.layer = LayerMask.NameToLayer("Objects");
        material = new Material(Shader.Find("Sprites/Default"));
        material.mainTexture = Texture2D.whiteTexture;
        BuildFace();
        scan = Child(root.transform,"SweepSector").transform;
        BuildSweep();
        pulse = Circle("PingWave",Radius,new Color(.4f,1f,.55f,.4f),.025f,59);
        for (int i = 0; i < dots.Length; i++)
        {
            var dot = Child(root.transform,"Signal"+i);
            Disk(dot.transform,"Glow",.28f,new Color(.3f,1f,.45f,.2f),.02f);
            Disk(dot.transform,"Rim",.16f,new Color(.015f,.09f,.04f,.98f),.01f);
            Disk(dot.transform,"Light",.105f,new Color(.82f,1f,.56f),-.01f);
            dot.SetActive(false); dots[i] = dot;
        }
        sound = SonarmanSound.Create(root);
        Ping();
    }
    private GameObject Child(Transform parent,string name)
    {
        var obj = new GameObject(name); obj.layer = root.layer;
        obj.transform.SetParent(parent,false);
        return obj;
    }
    private void MeshObject(Transform parent,string name,Vector3[] vertices,Color[] colors,int[] indices,int order)
    {
        var obj = Child(parent,name);
        var mesh = new Mesh { name = name, vertices = vertices, colors = colors, triangles = indices };
        mesh.RecalculateBounds(); meshes.Add(mesh);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material; renderer.sortingOrder = order;
    }
    private void BuildFace()
    {
        var vertices = new Vector3[97]; var colors = new Color[97]; var indices = new int[96*3];
        colors[0] = new Color(.015f,.075f,.035f,.18f);
        for (int i = 0; i < 96; i++)
        {
            float angle = i*Mathf.PI*2/96;
            vertices[i+1] = new(Mathf.Cos(angle)*Radius,Mathf.Sin(angle)*Radius,0);
            colors[i+1] = new Color(.015f,.075f,.035f,.4f);
            int t = i*3; indices[t] = 0; indices[t+1] = i+1; indices[t+2] = (i+1)%96+1;
        }
        MeshObject(root.transform,"RadarFace",vertices,colors,indices,54);
        Circle("OuterRimShadow",Radius,new Color(.01f,.08f,.035f,.9f),.065f,57);
        Circle("OuterRim",Radius,new Color(.45f,1f,.56f,.95f),.026f,58);
        Circle("RangeRing1",Radius/3,new Color(.3f,1f,.45f,.28f),.012f,57);
        Circle("RangeRing2",Radius*2/3,new Color(.3f,1f,.45f,.35f),.012f,57);
        for (int i = 0; i < 4; i++)
        {
            float angle = i*Mathf.PI/2;
            var direction = new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
            var line = Line(root.transform,"Axis"+i,new Color(.3f,1f,.45f,.25f),.012f,2,57);
            line.SetPosition(0,direction*.17f); line.SetPosition(1,direction*Radius);
        }
        BuildTicks();
    }
    private void BuildTicks()
    {
        var vertices = new Vector3[36*4]; var colors = new Color[vertices.Length]; var indices = new int[36*6];
        for (int i = 0; i < 36; i++)
        {
            float a = i*10*Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
            var side = new Vector3(-direction.y,direction.x,0)*.007f;
            float length = i%9 == 0 ? .1f : .047f;
            var inner = direction*(Radius-length); var outer = direction*(Radius+.015f);
            int v = i*4,t = i*6;
            vertices[v] = inner-side; vertices[v+1] = inner+side; vertices[v+2] = outer-side; vertices[v+3] = outer+side;
            for (int c = 0; c < 4; c++) colors[v+c] = new Color(.5f,1f,.6f,.8f);
            indices[t] = v; indices[t+1] = v+1; indices[t+2] = v+2;
            indices[t+3] = v+1; indices[t+4] = v+3; indices[t+5] = v+2;
        }
        MeshObject(root.transform,"BearingTicks",vertices,colors,indices,58);
    }
    private void BuildSweep()
    {
        const int segments = 36;
        var vertices = new Vector3[segments*3]; var colors = new Color[vertices.Length]; var indices = new int[vertices.Length];
        for (int i = 0; i < segments; i++)
        {
            float a = i*65f/segments*Mathf.Deg2Rad, b = (i+1)*65f/segments*Mathf.Deg2Rad;
            float fadeA = Mathf.Pow(1f-(float)i/segments,1.7f), fadeB = Mathf.Pow(1f-(float)(i+1)/segments,1.7f);
            int v = i*3;
            vertices[v] = Vector3.zero; vertices[v+1] = new(Mathf.Cos(a)*Radius,Mathf.Sin(a)*Radius,0);
            vertices[v+2] = new(Mathf.Cos(b)*Radius,Mathf.Sin(b)*Radius,0);
            colors[v] = new Color(.3f,1f,.45f,.12f*(fadeA+fadeB));
            colors[v+1] = new Color(.3f,1f,.45f,.5f*fadeA); colors[v+2] = new Color(.3f,1f,.45f,.5f*fadeB);
            for (int j = 0; j < 3; j++) indices[v+j] = v+j;
        }
        MeshObject(scan,"SweepFill",vertices,colors,indices,56);
        var halo = Line(scan,"SweepGlow",new Color(.35f,1f,.45f,.2f),.085f,2,59);
        var head = Line(scan,"SweepHead",new Color(.7f,1f,.65f,.9f),.025f,2,60);
        foreach (var line in new[] { halo,head })
        { line.SetPosition(0,Vector3.zero); line.SetPosition(1,new Vector3(Radius,0,0)); }
    }
    private LineRenderer Line(Transform parent,string name,Color color,float width,int points,int order)
    {
        var line = Child(parent,name).AddComponent<LineRenderer>();
        line.sharedMaterial = material; line.useWorldSpace = false; line.positionCount = points;
        line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
        line.sortingOrder = order;
        return line;
    }
    private LineRenderer Circle(string name,float radius,Color color,float width,int order)
    {
        var line = Line(root.transform,name,color,width,96,order); line.loop = true;
        for (int i = 0; i < 96; i++)
        {
            float a = i*Mathf.PI*2/96;
            line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0));
        }
        return line;
    }
    private void Disk(Transform parent,string name,float size,Color color,float z)
    {
        var obj = Child(parent,name);
        obj.transform.localScale = Vector3.one*size; obj.transform.localPosition = new(0,0,z);
        var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = Disc();
        renderer.sharedMaterial = material; renderer.color = color; renderer.sortingOrder = 61;
    }
    private static Sprite Disc()
    {
        if (disc != null) return disc;
        var texture = new Texture2D(32,32,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float dx = x-15.5f,dy = y-15.5f;
                texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(15.5f-Mathf.Sqrt(dx*dx+dy*dy))));
            }
        texture.Apply(false,true);
        return disc = Sprite.Create(texture,new(0,0,32,32),new(.5f,.5f),32f);
    }
    public void Ping() => SonarmanSound.Ping(sound);
    public void Update(float x,float y,float phase,IReadOnlyList<SonarmanMark> marks)
    {
        if (root == null) return;
        root.transform.position = new Vector3(x,y+.25f,-.5f);
        scan.localEulerAngles = new(0,0,90f-phase*360f);
        float wave = .08f+phase*.92f;
        pulse.transform.localScale = new(wave,wave,1);
        pulse.startColor = pulse.endColor = new Color(.4f,1f,.55f,.35f*(1f-phase));
        for (int i = 0; i < dots.Length; i++)
        {
            bool visible = i < marks.Count;
            dots[i].SetActive(visible);
            if (visible) dots[i].transform.localPosition = new Vector3((float)marks[i].X*Radius,(float)marks[i].Y*Radius,-.03f);
        }
    }
    public void Dispose()
    {
        if (sound != null) sound.Stop();
        if (root != null) UnityEngine.Object.Destroy(root);
        foreach (var mesh in meshes) UnityEngine.Object.Destroy(mesh);
        meshes.Clear(); UnityEngine.Object.Destroy(material);
    }
}
