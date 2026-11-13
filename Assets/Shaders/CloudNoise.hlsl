            float3 HashCell(float3 cell)
            {
                float3 hash = frac(cell * float3(0.1031, 0.1030, 0.0973));
                hash += dot(hash, hash.yxz + 33.33);
                return frac((hash.xxy + hash.yxx) * hash.zyx);
            }

            float PerlinNoise(float3 position)
            {
                float3 cell = floor(position);
                float3 offset = frac(position);
                float3 fade = offset * offset * offset * (offset * (offset * 6.0 - 15.0) + 10.0);
                float noise = 0.0;

                [unroll]
                for (int corner = 0; corner < 8; corner++)
                {
                    float3 vertex = float3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
                    float3 gradient = HashCell(cell + vertex) * 2.0 - 1.0;
                    float3 weight = lerp(1.0 - fade, fade, vertex);
                    noise += weight.x * weight.y * weight.z * dot(gradient, offset - vertex);
                }

                return saturate(0.5 + noise);
            }

            float PerlinFbm(float3 position)
            {
                float value = 0.0;
                float amplitude = 0.5;
                [unroll]
                for (int octave = 0; octave < 3; octave++)
                {
                    value += amplitude * PerlinNoise(position);
                    position = position * 2.03 + float3(5.2, 1.3, 8.7);
                    amplitude *= 0.5;
                }

                return value / 0.875;
            }

