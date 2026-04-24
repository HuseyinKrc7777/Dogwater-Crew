using Unity.Netcode;
using UnityEngine;

public static class GerstnerWaveDisplacement
{
    private static Vector3 GerstnerWave(Vector3 position, float steepness, float wavelength, float speed, float direction)
    {
        if (wavelength <= 0f) return Vector3.zero;

        direction = direction * 2 - 1;
        Vector2 d = new Vector2(Mathf.Cos(Mathf.PI * direction), Mathf.Sin(Mathf.PI * direction)).normalized;
        float k = 2 * Mathf.PI / wavelength;
        float a = steepness / k;
        float f = k * (Vector2.Dot(d, new Vector2(position.x, position.z)) - speed * (float)NetworkManager.Singleton.ServerTime.Time);

        return new Vector3(d.x * a * Mathf.Cos(f), a * Mathf.Sin(f), d.y * a * Mathf.Cos(f));
    }

    public static Vector3 GetWaveDisplacement(Vector3 position, float[] steepness, float[] wavelength, float[] speed, float[] directions)
    {
        Vector3 offset = Vector3.zero;

        offset += GerstnerWave(position, steepness[0], wavelength[0], speed[0], directions[0]);
        offset += GerstnerWave(position, steepness[1], wavelength[1], speed[1], directions[1]);
        offset += GerstnerWave(position, steepness[2], wavelength[2], speed[2], directions[2]);
        offset += GerstnerWave(position, steepness[3], wavelength[3], speed[3], directions[3]);

        return offset;
    }
}