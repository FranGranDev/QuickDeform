using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

[RequireComponent(typeof(MeshCollider), typeof(Rigidbody))]
public class Deform : MonoBehaviour
{
    private const int MAX_CONTACT_PER_COLLISION = 2;
    private const int MAX_COLLISION_PER_FRAME = 1;

    [Header("Settings")]
    [Range(0, 10), SerializeField] private float deformRadius = 0.2f;
    [Range(0, 10), SerializeField] private float maxDeform = 0.1f;
    [Range(0, 1), SerializeField] private float damageFalloff = 1;
    [Range(0, 10), SerializeField] private float damageMultiplier = 1;
    [Range(0, 2), SerializeField] private float minDamage = 0.1f;
    [SerializeField, Range(1, 10)] private int simplifyInit = 1;

    private DeformCalculation calculation;

    private new Rigidbody rigidbody;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    private Vector3[] startingVerticies;
    private Vector3[] meshVerticies;
    private bool[] meshVerticiesState;
    private List<Part>[] vertexChildParts;

    public List<Part> getChildParts => childParts;
    private List<Part> childParts;

    public bool DamageDisabled
    {
        get => damageDisabled;
        set
        {
            damageDisabled = value;
            if (childParts == null)
                return;
            foreach (Part part in childParts)
            {
                part.DamageDisabled = value;
            }
        }
    }
    private bool damageDisabled;

    private Action onCollision;
    private Action onMaxDeform;

    private int collisionCount;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        rigidbody = GetComponent<Rigidbody>();

        Init();
    }
    public void Init()
    {
        if (!gameObject.activeSelf)
            return;

        startingVerticies = meshFilter.mesh.vertices;
        meshVerticies = meshFilter.mesh.vertices;
        meshVerticiesState = new bool[meshVerticies.Length];

        GetChildParts();

        DeformCalculation.Settings settings = new DeformCalculation.Settings(deformRadius, maxDeform, damageFalloff, damageMultiplier, minDamage);
        calculation = gameObject.AddComponent<DeformCalculation>();
        calculation.Init(meshVerticies, meshVerticiesState, settings);
    }
    private void GetChildParts()
    {
        childParts = new List<Part>(GetComponentsInChildren<Part>());
        vertexChildParts = new List<Part>[meshVerticies.Length];

        for (int i = 0; i < meshVerticies.Length; i += simplifyInit)
        {
            vertexChildParts[i] = new List<Part>();

            foreach (Part part in childParts)
            {
                float dst = (part.transform.localPosition - meshVerticies[i]).magnitude;
                if (dst < part.MinDistance)
                {
                    vertexChildParts[i].Add(part);
                }
                if (part.HasCollider)
                {
                    Physics.IgnoreCollision(meshCollider, part.GetCollider, true);
                }
            }
        }


    }

    private void OnCollisionEnter(Collision collision)
    {
        if (DamageDisabled || collisionCount > MAX_COLLISION_PER_FRAME)
            return;
        if (calculation != null)
        {
            QuickDeform(collision);
            collisionCount++;
        }
    }
    private void OnCollisionStay(Collision collision)
    {
        if (DamageDisabled || collisionCount > MAX_COLLISION_PER_FRAME)
            return;
        if (calculation != null)
        {
            QuickDeform(collision);
            collisionCount++;
        }
    }
    private void LateUpdate()
    {
        collisionCount = 0;
    }

    private void SlowDeform(Collision collision)
    {
        float staticObjRatio = collision.rigidbody ? 1 : 0.5f;
        float impulse = Mathf.Clamp((collision.impulse.magnitude) / rigidbody.mass * staticObjRatio * 0.25f, 0, 1.25f);

        float sqrDeformRadius = deformRadius * deformRadius;
        float sqrMaxDeform = maxDeform * maxDeform;

        if (impulse > minDamage)
        {

            for (int c = 0; c < collision.contactCount; c++)
            {
                Profiler.BeginSample("Collision: " + collision.transform.name);

                ContactPoint point = collision.contacts[c];
                for (int i = 0; i < meshVerticies.Length; i++)
                {
                    Profiler.BeginSample("Calculate point");
                    Vector3 vertexPosition = meshVerticies[i];
                    Vector3 pointPosition = transform.InverseTransformPoint(point.point);
                    Profiler.EndSample();
                    Profiler.BeginSample("Calculate distance and dot");
                    float sqrDistanceFromCollision = (vertexPosition - pointPosition).sqrMagnitude;
                    float sqrDistanceFromOriginal = (startingVerticies[i] - vertexPosition).sqrMagnitude;
                    float velocityDot = Mathf.Abs(Vector3.Dot(collision.relativeVelocity.normalized, point.normal) * 0.5f) + 0.5f;
                    Profiler.EndSample();
                    if (sqrDistanceFromCollision < sqrDeformRadius)
                    {
                        if (sqrDistanceFromOriginal < sqrMaxDeform)
                        {
                            float falloff = (1 - Mathf.Sqrt(sqrDistanceFromCollision / sqrDeformRadius) * damageFalloff);

                            float xDeform = pointPosition.x * falloff;
                            float yDeform = pointPosition.y * falloff;
                            float zDeform = pointPosition.z * falloff;

                            xDeform = Mathf.Clamp(xDeform, 0f, maxDeform);
                            yDeform = Mathf.Clamp(yDeform, 0f, maxDeform);
                            zDeform = Mathf.Clamp(zDeform, 0f, maxDeform);

                            Vector3 deform = new Vector3(xDeform, yDeform, zDeform);

                            meshVerticies[i] -= deform * damageMultiplier * impulse * velocityDot;
                        }
                        else
                        {
                            onMaxDeform?.Invoke();
                        }
                        Profiler.BeginSample("Finding Part");
                        if (vertexChildParts[i].Count > 0)
                        {
                            foreach (Part part in vertexChildParts[i])
                            {
                                part.GetDamage(rigidbody.velocity);
                            }
                        }
                        Profiler.EndSample();
                    }

                }

                Profiler.EndSample();
            }

            onCollision?.Invoke();
            Profiler.BeginSample("Mesh Update");
            UpdateMeshVerticies();
            Profiler.EndSample();


        }
    } //Singlethreading
    private void QuickDeform(Collision collision)
    {
        float roadRatio = collision.rigidbody ? 1 : 0.2f;
        float impulse = Mathf.Clamp((collision.impulse.magnitude) / rigidbody.mass * 0.25f, 0, 1.25f);

        if (impulse * roadRatio > minDamage)
        {
            for (int i = 0; i < collision.contactCount && i < MAX_CONTACT_PER_COLLISION; i++)
            {
                float velocityDot = Mathf.Abs(Vector3.Dot(collision.relativeVelocity.normalized, collision.contacts[i].normal));

                DeformCalculation.Output data = calculation.Calculate(impulse, velocityDot, transform.worldToLocalMatrix, collision.contacts[i]);

                meshVerticies = data.Verticies;
                meshVerticiesState = data.VerticiesState;

                for (int a = 0; a < meshVerticies.Length; a++)
                {
                    if (data.VerticiesDamaged[a] && vertexChildParts[a] != null)
                    {
                        foreach (Part part in vertexChildParts[a])
                        {
                            part.GetDamage(rigidbody.velocity, simplifyInit);
                        }
                    }
                }
            }
            for (int a = 0; a < meshVerticies.Length; a++)
            {
                if (meshVerticiesState[a])
                {
                    onMaxDeform?.Invoke();
                }
            }

            onCollision?.Invoke();
            UpdateMeshVerticies();
        }
    } //Multithreading

    private void UpdateMeshVerticies()
    {
        meshFilter.mesh.vertices = meshVerticies;
        meshCollider.sharedMesh = meshFilter.mesh;
    }

    public void SubcribeForCollision(Action action, bool subcribe = true)
    {
        if (subcribe)
        {
            onCollision += action;
        }
        else
        {
            onCollision -= action;
        }
    }
    public void SubcribeForCrash(Action action, bool subcribe = true)
    {
        if (subcribe)
        {
            onMaxDeform += action;
        }
        else
        {
            onMaxDeform -= action;
        }
    }
}