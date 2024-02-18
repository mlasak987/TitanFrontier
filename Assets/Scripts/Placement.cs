using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class Placement : MonoBehaviour
{
    public GameObject prefab;
    public Camera cam;

    bool CheckRaySphere(Ray ray, float sphereRadius, out float distance)
    {
        Vector3 localPoint = ray.origin;
        float temp = -Vector3.Dot(localPoint, ray.direction);
        float det = temp * temp - Vector3.Dot(localPoint, localPoint) + sphereRadius * sphereRadius;
        if (det < 0) { distance = Mathf.Infinity; return false; }
        det = Mathf.Sqrt(det);
        float intersection0 = temp - det;
        float intersection1 = temp + det;
        if (intersection0 >= 0)
        {
            if (intersection1 >= 0)
            {
                distance = Mathf.Min(intersection0, intersection1); return true;
            }
            else
            {
                distance = intersection0; return true;
            }
        }
        else if (intersection1 >= 0)
        {
            distance = intersection1; return true;
        }
        else
        {
            distance = Mathf.Infinity; return false;
        }
    }

    Vector3 TranslateCoords(Vector3 pos)
    {
        Ray ray = new(pos, pos + Vector3.one);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            return hit.transform.position;
        }

        return Vector3.zero;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (CheckRaySphere(ray, 25740, out float distance))
            {
                // did hit sphere.
                Vector3 position = ray.GetPoint(distance);
                Vector3 normal = TranslateCoords(position.normalized);
                Quaternion rotation = Quaternion.LookRotation(normal);
                Instantiate(prefab, position, rotation);
            }
        }
    }
}
