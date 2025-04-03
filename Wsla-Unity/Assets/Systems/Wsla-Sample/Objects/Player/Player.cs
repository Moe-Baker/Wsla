using UnityEngine;

using Wsla;
using Wsla.Unity;

public partial class Player : NetworkBehaviour
{
    [SerializeField]
    float MovementSpeed = 5f;
    [SerializeField]
    float MovementAcceleration = 20f;

    [SerializeField]
    float RotationSpeed = 420f;

    [SerializeField]
    Rigidbody Rigidbody;

    [SerializeField]
    NetworkAnimator Animator;

    NetworkAnimatorMemberIndex MoveIndex;

    Vector3 Velocity
    {
        get => Rigidbody.linearVelocity;
        set => Rigidbody.linearVelocity = value;
    }

    public override void Set(NetworkEntity.Behaviour reference)
    {
        base.Set(reference);

        MoveIndex = Animator.IndexFloat("Move");

        Network.Entity.OnSpawn += SpawnCallback;
    }

    void SpawnCallback()
    {
        NetworkLog.Info($"Player {Network.Entity.ID} Spawned");
    }

    void Update()
    {
        if (Network.Entity.IsRemote)
            return;

        Move();
    }
    void Move()
    {
        var input = GetInput();

        var horizontal = ((Vector3.forward * input.y) + (Vector3.right * input.x)) * MovementSpeed;
        var vertical = Vector3.up * Velocity.y;

        Velocity = Vector3.MoveTowards(Velocity, horizontal + vertical, MovementAcceleration * Time.deltaTime);

        Rotate(horizontal);

        Animate(Velocity);
    }
    void Rotate(Vector3 direction)
    {
        if (direction.magnitude < 0.2f)
            return;

        var rotation = Quaternion.LookRotation(direction);
        Rigidbody.rotation = Quaternion.RotateTowards(Rigidbody.rotation, rotation, RotationSpeed * Time.deltaTime);
    }
    void Animate(Vector3 velocity)
    {
        velocity.y = 0f;

        var value = velocity.magnitude / MovementSpeed;
        Animator.SetFloat(MoveIndex, value);
    }

    Vector2 GetInput()
    {
        var input = Vector2.zero;

        if (Input.GetKey(KeyCode.W))
        {
            input.y += 1;
        }
        if (Input.GetKey(KeyCode.S))
        {
            input.y -= 1;
        }

        if (Input.GetKey(KeyCode.D))
        {
            input.x += 1;
        }
        if (Input.GetKey(KeyCode.A))
        {
            input.x -= 1;
        }

        return input.normalized;
    }
}