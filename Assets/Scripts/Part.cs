using UnityEngine;

public abstract class Part : MonoBehaviour
{
    public const float MAX_VELOCITY = 25;

    [Header("Deform Settings")]
    [Range(1, 5000), SerializeField] protected int maxHp;
    [Range(0, 5), SerializeField] private protected float minDstToParent;
    [Range(0, 1f), SerializeField] protected float randomizeDemolish;
    [Range(0, 1f), SerializeField] protected float drag = 0f;

    public int MaxHp => maxHp;
    public int Hp { get; private set; }
    public float HpProcent
    {
        get
        {
            return (float)Hp / (float)maxHp;
        }
    }
    public bool Demolished { get; private set; }
    public float MinDistance => minDstToParent;
    public bool HasCollider => collider != null;
    public virtual bool DamageDisabled { get; set; }


    protected new Rigidbody rigidbody;
    protected new Collider collider;
    public virtual Collider GetCollider => collider;
    public virtual Rigidbody GetRigidbody => rigidbody;

    public void GetDamage(Vector3 velocity, int damage = 1)
    {
        if (Demolished || DamageDisabled)
            return;
        Hp -= damage;
        if(Hp <= 0)
        {
            Demolish(velocity);
            Demolished = true;
        }
        else
        {
            OnGetDamage();
        }
    }

    public abstract void Demolish(Vector3 velocity);
    protected virtual void OnGetDamage()
    {

    }
    public virtual void GetBaseComponents()
    {

    }


    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        collider = GetComponent<Collider>();

        GetBaseComponents();

        Hp = maxHp;
    }
}
