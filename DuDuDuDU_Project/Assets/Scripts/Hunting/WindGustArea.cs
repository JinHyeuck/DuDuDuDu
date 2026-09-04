using UnityEngine;

namespace OJ.Hunting
{
    public class WindGustArea : MonoBehaviour
    {
        private Vector2 _direction = Vector2.down;
        private float _speed;
        private float _duration;
        private float _maxDistance;
        private float _pushPerSecond;
        private float _elapsed;
        private float _traveled;

        public void Init(
            Vector2 startPosition,
            Vector2 direction,
            float halfLength,
            float halfWidth,
            float duration,
            float travelDistance,
            float pushPerSecond)
        {
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
            _duration = Mathf.Max(0.1f, duration);
            _maxDistance = Mathf.Max(0.1f, travelDistance);
            _speed = _maxDistance / _duration;
            _pushPerSecond = Mathf.Max(0f, pushPerSecond);
            _elapsed = 0f;
            _traveled = 0f;

            transform.position = startPosition;
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box == null)
                box = gameObject.AddComponent<BoxCollider2D>();

            box.isTrigger = true;
            box.size = new Vector2(Mathf.Max(0.1f, halfWidth * 2f), Mathf.Max(0.1f, halfLength * 2f));

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body == null)
                body = gameObject.AddComponent<Rigidbody2D>();

            body.isKinematic = true;
            body.gravityScale = 0f;
            body.simulated = true;
        }

        private void Update()
        {
            float step = _speed * Time.deltaTime;
            transform.position += (Vector3)(_direction * step);

            _traveled += step;
            _elapsed += Time.deltaTime;

            if (_elapsed >= _duration || _traveled >= _maxDistance)
                Destroy(gameObject);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (other == null || !other.CompareTag("Monster"))
                return;

            Monster monster = other.GetComponent<Monster>();
            if (monster == null || !monster.gameObject.activeInHierarchy)
                return;

            monster.PushBy(_direction, _pushPerSecond * Time.deltaTime);
        }
    }
}
