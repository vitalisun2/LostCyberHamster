using UnityEngine;

public class EnergyShieldUlta : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float alphaChangeSpeed = 1f;  // Speed of alpha change
    private float minAlpha = 0.1f;        // Minimum alpha value
    private float maxAlpha = 0.5f;          // Maximum alpha value
    private bool isIncreasing = true;     // Flag to toggle between increasing and decreasing
    private float rotationSpeed = 50;   // Speed of rotation

    void Start()
    {
        // Get the SpriteRenderer component from the child object
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (spriteRenderer == null)
        {
            Debug.LogError("SpriteRenderer component not found.");
            return;
        }

        // Handle the glowing effect
        HandleGlowingEffect();

        // Rotate the object around Z-axis
        RotateObject();
    }

    // Separate method for handling glowing effect
    private void HandleGlowingEffect()
    {
        Color color = spriteRenderer.color;

        // Adjust alpha based on whether we're increasing or decreasing
        if (isIncreasing)
        {
            color.a += alphaChangeSpeed * Time.deltaTime;
            if (color.a >= maxAlpha)
            {
                color.a = maxAlpha;
                isIncreasing = false; // Switch to decreasing
            }
        }
        else
        {
            color.a -= alphaChangeSpeed * Time.deltaTime;
            if (color.a <= minAlpha)
            {
                color.a = minAlpha;
                isIncreasing = true; // Switch to increasing
            }
        }

        // Apply the new color to the sprite
        spriteRenderer.color = color;
    }

    // Method to rotate the GameObject around the Z-axis
    private void RotateObject()
    {
        spriteRenderer.transform.Rotate(0, 0, -rotationSpeed * Time.deltaTime);
    }
}
