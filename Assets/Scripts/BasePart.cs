using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasePart : Part
{
    public override void Demolish(Vector3 velocity)
    {
        rigidbody.isKinematic = false;

        Vector3 impulse = (velocity.normalized + Random.onUnitSphere * randomizeDemolish).normalized * Mathf.Clamp(velocity.magnitude, 0, MAX_VELOCITY) * (1 - drag);
        Vector3 angular = Random.onUnitSphere * 180 * randomizeDemolish;


        rigidbody.AddForce(impulse, ForceMode.VelocityChange);
        rigidbody.AddTorque(angular, ForceMode.VelocityChange);

        transform.parent = null;
        collider.enabled = true;
    }
}
