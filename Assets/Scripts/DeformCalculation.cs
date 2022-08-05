using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;


public class DeformCalculation : MonoBehaviour
{    
    private const int CORE_COUNT = 4;

    private NativeArray<Vector3> verticies;
    private NativeArray<Vector3> startVerticies;
    private NativeArray<bool> verticiesState;

    private Settings settings;

    private bool disposed = false;

    public void Init(Vector3[] mesh, bool[] state, Settings settings)
    {
        verticies = new NativeArray<Vector3>(mesh, Allocator.Persistent);
        startVerticies = new NativeArray<Vector3>(mesh, Allocator.Persistent);
        verticiesState = new NativeArray<bool>(state, Allocator.Persistent);

        this.settings = settings;
    }
    private void Dispose()
    {
        if (disposed)
            return;
        verticies.Dispose();
        startVerticies.Dispose();
        verticiesState.Dispose();
        disposed = true;
    }

    public Output Calculate(float impulse, float velocityDot, Matrix4x4 matrix, ContactPoint point)
    {
        DeformJob job = new DeformJob()
        {
            Verticies = verticies,
            StartVerticies = startVerticies,
            OutputVerticiesState = verticiesState,
            MatrixWorldToLocal = matrix,
            DeformData = settings,
            Point = point,
            Impulse = impulse,
            VelocityDot = velocityDot,

            OutputVerticies = new NativeArray<Vector3>(verticies, Allocator.TempJob),
            OutputVerticiesDamaged = new NativeArray<bool>(verticiesState, Allocator.TempJob),
        };

        JobHandle handle = job.Schedule(verticies.Length, CORE_COUNT);
        handle.Complete();

        bool[] damagedVerticies = new bool[verticies.Length];

        job.OutputVerticies.CopyTo(verticies);
        job.OutputVerticiesState.CopyTo(verticiesState);
        job.OutputVerticiesDamaged.CopyTo(damagedVerticies);

        job.OutputVerticies.Dispose();
        job.OutputVerticiesDamaged.Dispose();

        return new Output(verticies, verticiesState, damagedVerticies);
    }

    public struct Output
    {
        readonly public Vector3[] Verticies;
        readonly public bool[] VerticiesState; 
        readonly public bool[] VerticiesDamaged; 

        public Output(NativeArray<Vector3> verticies, NativeArray<bool> destroyed, bool[] damaged)
        {
            Verticies = verticies.ToArray();
            VerticiesState = destroyed.ToArray();
            VerticiesDamaged = damaged;
        }
    }
    public struct Settings
    {
        readonly public float sqrDeformRadius;
        readonly public float sqrMaxDeform;
        readonly public float damageFalloff;
        readonly public float damageMultiplier;
        readonly public float minDamage;

        public Settings(float deformRadius, float maxDeform, float damageFalloff, float damageMultiplier, float minDamage)
        {
            this.sqrDeformRadius = deformRadius * deformRadius;
            this.sqrMaxDeform = maxDeform * maxDeform;
            this.damageFalloff = damageFalloff;
            this.damageMultiplier = damageMultiplier;
            this.minDamage = minDamage;
        }
    }
    public struct DeformJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector3> Verticies;
        [ReadOnly] public NativeArray<Vector3> StartVerticies;
        [ReadOnly] public Matrix4x4 MatrixWorldToLocal;
        [ReadOnly] public Settings DeformData;
        [ReadOnly] public ContactPoint Point;
        [ReadOnly] public float Impulse;
        [ReadOnly] public float VelocityDot;

        [WriteOnly] public NativeArray<bool> OutputVerticiesState;
        [WriteOnly] public NativeArray<Vector3> OutputVerticies;
        [WriteOnly] public NativeArray<bool> OutputVerticiesDamaged;

        public void Execute(int index)
        {
            Vector3 vertexPosition = Verticies[index];
            Vector3 pointPosition = MatrixWorldToLocal.MultiplyPoint3x4(Point.point);

            float distanceFromCollision = (vertexPosition - pointPosition).sqrMagnitude;
            float distanceFromOriginal = (StartVerticies[index] - vertexPosition).sqrMagnitude;

            if (distanceFromCollision < DeformData.sqrDeformRadius)
            {
                if (distanceFromOriginal < DeformData.sqrMaxDeform)
                {
                    float falloff = (1 - Mathf.Sqrt(distanceFromCollision / DeformData.sqrDeformRadius) * DeformData.damageFalloff);

                    float xDeform = pointPosition.x * falloff;
                    float yDeform = pointPosition.y * falloff;
                    float zDeform = pointPosition.z * falloff;

                    xDeform = Mathf.Clamp(xDeform, 0f, DeformData.sqrMaxDeform);
                    yDeform = Mathf.Clamp(yDeform, 0f, DeformData.sqrMaxDeform);
                    zDeform = Mathf.Clamp(zDeform, 0f, DeformData.sqrMaxDeform);

                    Vector3 deform = new Vector3(xDeform, yDeform, zDeform);

                    OutputVerticies[index] = Verticies[index] - deform * DeformData.damageMultiplier * Impulse;
                }
                else
                {
                    OutputVerticiesState[index] = true;
                }

                OutputVerticiesDamaged[index] = true;
            }
        }
    }

    private void OnDestroy()
    {
        Dispose();
    }
    private void OnApplicationQuit()
    {
        Dispose();
    }
}
