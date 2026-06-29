using UnityEngine;
using System.Collections.Generic;

namespace Lightbug.LaserMachine
{
    public class LaserMachine : MonoBehaviour
    {
        struct LaserElement
        {
            public Transform transform;
            public LineRenderer lineRenderer;
            public GameObject sparks;
            public bool impact;
        }

        [Header("Layer")]
        [SerializeField] private bool m_setLayer = false;

        [SerializeField]
        private LaserLayerType m_layerType = LaserLayerType.Minimap;

        private enum LaserLayerType
        {
            Minimap,
            MinimapIgnore
        }
        private readonly List<LaserElement> elementsList = new List<LaserElement>();

        [Header("External Data")]
        [SerializeField] private LaserData m_data;

        [Tooltip("This variable is true by default, all the inspector properties will be overridden.")]
        [SerializeField] private bool m_overrideExternalProperties = true;

        [SerializeField] private LaserProperties m_inspectorProperties = new LaserProperties();

        [Header("Force Single Static Beam")]
        [SerializeField] private bool m_forceSingleRay = true;

        [SerializeField] private bool m_disableRotation = true;

        private LaserProperties m_currentProperties;
        private float m_time = 0f;
        private bool m_active = true;
        private bool m_assignLaserMaterial;
        private bool m_assignSparks;

        void OnEnable()
        {
            elementsList.Clear();

            if (m_overrideExternalProperties)
            {
                m_currentProperties = m_inspectorProperties;
            }
            else
            {
                if (m_data == null)
                {
                    Debug.LogError("[LaserMachine] LaserData is null.");
                    return;
                }

                m_currentProperties = m_data.m_properties;
            }

            if (m_currentProperties == null)
            {
                Debug.LogError("[LaserMachine] LaserProperties is null.");
                return;
            }

            m_currentProperties.m_initialTimingPhase = Mathf.Clamp01(m_currentProperties.m_initialTimingPhase);
            m_time = m_currentProperties.m_initialTimingPhase * m_currentProperties.m_intervalTime;

            m_assignSparks = (m_data != null && m_data.m_laserSparks != null);
            m_assignLaserMaterial = (m_data != null && m_data.m_laserMaterial != null);

            int rayCount = m_forceSingleRay ? 1 : Mathf.Max(1, m_currentProperties.m_raysNumber);
            float angleStep = rayCount > 0 ? m_currentProperties.m_angularRange / rayCount : 0f;

            for (int i = 0; i < rayCount; i++)
            {
                LaserElement element = new LaserElement();

                GameObject newObj = new GameObject("lineRenderer_" + i);
                if (m_setLayer)
                {
                    switch (m_layerType)
                    {
                        case LaserLayerType.Minimap:
                            newObj.layer = LayerMask.NameToLayer("Minimap");
                            break;

                        case LaserLayerType.MinimapIgnore:
                            newObj.layer = LayerMask.NameToLayer("Default");
                            break;
                    }
                }
                if (m_currentProperties.m_physicsType == LaserProperties.PhysicsType.Physics2D)
                    newObj.transform.position = (Vector2)transform.position;
                else
                    newObj.transform.position = transform.position;

                newObj.transform.rotation = transform.rotation;

                if (rayCount > 1)
                    newObj.transform.Rotate(Vector3.up, i * angleStep);

                newObj.transform.position += newObj.transform.forward * m_currentProperties.m_minRadialDistance;

                LineRenderer lr = newObj.AddComponent<LineRenderer>();

                if (m_assignLaserMaterial)
                    lr.material = m_data.m_laserMaterial;

                lr.receiveShadows = false;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.startWidth = m_currentProperties.m_rayWidth;
                lr.endWidth = m_currentProperties.m_rayWidth;
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, newObj.transform.position);
                lr.SetPosition(1, newObj.transform.position + transform.forward * m_currentProperties.m_maxRadialDistance);

                newObj.transform.SetParent(transform, true);

                if (m_assignSparks)
                {
                    GameObject sparks = Instantiate(m_data.m_laserSparks);
                    sparks.transform.SetParent(newObj.transform, false);
                    sparks.SetActive(false);
                    element.sparks = sparks;
                }

                element.transform = newObj.transform;
                element.lineRenderer = lr;
                element.impact = false;

                elementsList.Add(element);
            }
        }

        void Update()
        {
            if (m_currentProperties == null || elementsList.Count == 0)
                return;

            if (m_currentProperties.m_intermittent)
            {
                m_time += Time.deltaTime;

                if (m_time >= m_currentProperties.m_intervalTime)
                {
                    m_active = !m_active;
                    m_time = 0f;
                }
            }

            RaycastHit2D hitInfo2D;
            RaycastHit hitInfo3D;

            for (int i = 0; i < elementsList.Count; i++)
            {
                LaserElement element = elementsList[i];

                if (!m_disableRotation && m_currentProperties.m_rotate)
                {
                    float rotateAmount = Time.deltaTime * m_currentProperties.m_rotationSpeed;
                    if (m_currentProperties.m_rotateClockwise)
                        element.transform.RotateAround(transform.position, transform.up, rotateAmount);
                    else
                        element.transform.RotateAround(transform.position, transform.up, -rotateAmount);
                }

                if (m_active)
                {
                    element.lineRenderer.enabled = true;
                    element.lineRenderer.SetPosition(0, element.transform.position);

                    if (m_currentProperties.m_physicsType == LaserProperties.PhysicsType.Physics3D)
                    {
                        Physics.Linecast(
                            element.transform.position,
                            element.transform.position + element.transform.forward * m_currentProperties.m_maxRadialDistance,
                            out hitInfo3D,
                            m_currentProperties.m_layerMask
                        );

                        if (hitInfo3D.collider != null)
                        {
                            element.lineRenderer.SetPosition(1, hitInfo3D.point);

                            if (m_assignSparks && element.sparks != null)
                            {
                                element.sparks.transform.position = hitInfo3D.point;
                                element.sparks.transform.rotation = Quaternion.LookRotation(hitInfo3D.normal);
                                element.sparks.SetActive(true);
                            }
                        }
                        else
                        {
                            element.lineRenderer.SetPosition(1, element.transform.position + element.transform.forward * m_currentProperties.m_maxRadialDistance);

                            if (m_assignSparks && element.sparks != null)
                                element.sparks.SetActive(false);
                        }
                    }
                    else
                    {
                        hitInfo2D = Physics2D.Linecast(
                            element.transform.position,
                            element.transform.position + element.transform.forward * m_currentProperties.m_maxRadialDistance,
                            m_currentProperties.m_layerMask
                        );

                        if (hitInfo2D.collider != null)
                        {
                            element.lineRenderer.SetPosition(1, hitInfo2D.point);

                            if (m_assignSparks && element.sparks != null)
                            {
                                element.sparks.transform.position = hitInfo2D.point;
                                element.sparks.transform.rotation = Quaternion.LookRotation(hitInfo2D.normal);
                                element.sparks.SetActive(true);
                            }
                        }
                        else
                        {
                            element.lineRenderer.SetPosition(1, element.transform.position + element.transform.forward * m_currentProperties.m_maxRadialDistance);

                            if (m_assignSparks && element.sparks != null)
                                element.sparks.SetActive(false);
                        }
                    }
                }
                else
                {
                    element.lineRenderer.enabled = false;

                    if (m_assignSparks && element.sparks != null)
                        element.sparks.SetActive(false);
                }

                elementsList[i] = element;
            }
        }
    }
}