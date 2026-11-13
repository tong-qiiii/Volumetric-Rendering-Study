
## 十一 积云密度缓存与性能优化

瓶颈与改动：原版在每次视线、太阳和天空采样时，都重新计算五组云体、五十个鼓包和多层噪声。使用同一份 HLSL 密度函数，通过 Compute Shader 预先生成 256×128×256 的 RHalf 三维纹理，运行时以线性插值读取密度。原始纹理数据约 16 MiB；光照与遮挡仍实时计算，云体漂移仍有效。修改云团生成公式后需要重新烘焙。

默认 Balanced 采用缓存密度和 96/8/3 采样预算；保留太阳、天空采样数以维持云底明暗。High 为 128/8/3，Fast 为 64/6/2，按 F2 切换。森林不启用此缓存。生成器和运行时统一使用默认预算，避免进入 Play 后退回慢版本。

固定日落镜头、冻结风速，每档预热 2 次、测量 5 次取中位数。测量包含 CPU 提交、GPU 完成和同步图像回读，不等于 GPU 单独耗时或游戏 FPS。QHD 平衡档相对原版降低约 89.9% 的测试耗时；关闭云的耗时说明测试本身含明显基础开销。

|方案|720p 中位毫秒|QHD 中位毫秒|
|---|---:|---:|
|原版程序密度|67.018|262.947|
|仅缓存，同预算|12.262|33.354|
|缓存平衡档|10.059|26.570|
|缓存快速档|10.343|24.463|
|关闭云|5.835|18.725|

质量检查：在原版与无云参考差异超过 2 的云区，缓存同预算 RGB 平均绝对误差为 0.889/255，平衡档为 1.058/255。三时段和山脊深度诊断图已重新生成，遮挡有效像素为 123544。缓存插值会略微平滑最细小的云边；Fast 档质量损失更明显。

测试环境：Unity 6000.6.0f1、Direct3D11，运行时识别为 NVIDIA GeForce RTX 4070 Laptop GPU。用户描述的是桌面版 4070，不能将本环境数据直接当作其桌面显卡结果。独立构建、长时间移动、不同镜头与显卡温度仍需进一步验证。

实际 Play 测量记录：Play mode Editor frame intervals: 30 warmup frames + 90 samples; median=6.497 ms; p95=7.918 ms; camera=2560x1440; GPU=NVIDIA GeForce RTX 4070 Laptop GPU; vSyncCount=0; targetFrameRate=-1. Includes Editor overhead; not standalone build performance; unchanged frame cap settings.

![图15 性能优化前后](../CumulusPerformance/Comparison.jpg)
