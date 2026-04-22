float3 GerstnerWave(float3 position, float steepness, float wavelength, float speed, float direction , float time, inout float3 tangent, inout float3 binormal)
{
    direction = direction * 2 - 1;
    float2 d = normalize(float2(cos(3.14 * direction), sin(3.14 * direction)));
    float k = 2 * 3.14 / wavelength;                                           
    float f = k * (dot(d, position.xz) - speed * time);
    float a = steepness / k;

    tangent += float3(
    -d.x * d.x * (steepness * sin(f)),
    d.x * (steepness * cos(f)),
    -d.x * d.y * (steepness * sin(f))
    );

    binormal += float3(
    -d.x * d.y * (steepness * sin(f)),
    d.y * (steepness * cos(f)),
    -d.y * d.y * (steepness * sin(f))
    );

    return float3(
    d.x * (a * cos(f)),
    a * sin(f),
    d.y * (a * cos(f))
    );
}

void GerstnerWaves_float(float3 position, float4 steepness, float4 wavelength, float4 speed, float4 directions, float time, out float3 Offset, out float3 normal )
{
    Offset = 0;
    
    float3 tangent = float3(1, 0, 0);
    float3 binormal = float3(0, 0, 1);

    Offset += GerstnerWave(position, steepness.x, wavelength.x, speed.x, directions.x , time , tangent, binormal );
    Offset += GerstnerWave(position, steepness.y, wavelength.y, speed.y, directions.y , time , tangent, binormal );
    Offset += GerstnerWave(position, steepness.z, wavelength.z, speed.z, directions.z , time , tangent, binormal );
    Offset += GerstnerWave(position, steepness.w, wavelength.w, speed.w, directions.w , time , tangent, binormal );

    normal = normalize(cross(binormal, tangent));
    //TBN = transpose(float3x3(tangent, binormal, normal));
}