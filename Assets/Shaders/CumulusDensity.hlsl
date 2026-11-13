            // World-metric lobes avoid stretching 3D noise into horizontal sheets.
            float CumulusDensity(float3 positionOS, float3 windOffset)
            {
                #if !defined(CUMULUS_BAKE)
                if (_UseBakedDensity > .5)
                {
                    float3 uv=positionOS+.5-sin(windOffset)*8/float3(330,120,300);
                    if(any(uv<0)||any(uv>1))return 0;
                    return SAMPLE_TEXTURE3D_LOD(_CumulusDensityTex,sampler_CumulusDensityTex,uv,0).r*_Density*_Coverage;
                }
                #endif
                float3 p = positionOS * float3(330,120,300) - sin(windOffset) * 8;
                float field = -1000;
                [loop] for(int group=0;group<5;group++)
                {
                    float3 center = group==0 ? float3(-65,-4,-60) : group==1 ? float3(48,-8,-45)
                        : group==2 ? float3(-36,6,28) : group==3 ? float3(66,-3,65) : float3(-75,-10,82);
                    float size = group==2 ? 1.1 : group==1 ? .85 : 1;
                    // A broad core and overlapping rising lobes form cauliflower silhouettes.
                    float3 q=(p-center)/size;
                    float d=22-length(q/float3(1.35,.85,1.15));
                    [loop] for(int lobe=0;lobe<10;lobe++)
                    {
                        float3 seed=HashCell(float3(group*7+lobe,13,41));
                        float angle=lobe*2.39996;
                        float3 offset=float3(cos(angle)*20,10+seed.y*18,sin(angle)*18);
                        float radius=10+seed.x*7;
                        d=max(d,radius-length(q-offset));
                    }
                    field=max(field,d*size);
                }
                float broad=(PerlinFbm(p*.075)-.5)*16;
                float fine=(PerlinNoise(p*.29+7)-.5)*3.5;
                float shape=smoothstep(-.5,3.5,field+broad+fine);
                float base=smoothstep(-37,-24,p.y);
                return _Density*shape*base*_Coverage;
            }

