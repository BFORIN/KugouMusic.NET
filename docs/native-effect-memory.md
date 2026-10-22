# 原生特效主题内存优化验证

2026-10-22，分支 `feat/native-gpu-effects`，对比基线 `07cc4a0`。

本次保留原始渲染分辨率、4 倍 MSAA、文字栅格倍率、所有视觉效果、256 MiB 纹理缓存和帧率配置。

## 修改

- Metal 的 2D 绘制目标用独立 S8 模板附件替代 Depth32Float/Stencil8。渲染器始终关闭深度测试与深度写入，模板裁剪仍保留；跨 render pass 的模板内容继续 store/load，没有改成 memoryless。
- 后处理金字塔、材质 ping/pong、光带、Latent 背景和 Tempera 的颜色解析目标不再分配不用的深度/模板附件。这部分也适用于 OpenGL/Vulkan。
- Metal/Vulkan 的交换链最多持有三张图像。正常情况下保留可用备用图像及原有复用顺序；全部仍在提交时，沿用画布的下一次合成回调重试，不阻塞 UI 或覆盖在途图像。缩放后旧图像完成提交才能释放。
- 交换链从创建开始就拥有图像，绘制或提交中途失败也不会遗漏统一释放；取消和失败的提交都可回收。

## 内存与画面

Apple M5、macOS、Release、Metal 离屏渲染；同一组八行歌词、同一 seed 和时钟序列，每个场景跑 120 帧（时钟步长 0.25 秒）。

下表为 3420×1994 时 `MTLDevice.currentAllocatedSize` 的观测值，包含探针输出目标和 Metal 分配开销，**不是播放器进程总内存**。

| 场景 | 基线 GPU 分配（MiB） | 优化后（MiB） |
| --- | ---: | ---: |
| Sonnet / 十四行诗 | 628.0 | 432.9 |
| Tempera / 凝彩 | 1022.0 | 713.6 |
| Lumiere / 绘光 | 187.5 | 160.3 |
| Latent / 动态背景 | 79.7 | 42.8 |

1710×997 和 3420×1994 两种尺寸，四个场景，每十帧读回一次 RGBA 像素，共 96 帧；修改前后每帧 SHA-256 全部一致。另有真实 Metal 测试覆盖：模板裁剪在切换目标后仍有效、4 倍 MSAA 的边缘部分覆盖保留、颜色解析目标不需要模板附件、目标缩放后仍正常。

## 帧耗时

离屏测量将 CPU 提交和等待 GPU 完成的总墙钟时间分开记录。每轮 120 帧，排除前 30 帧；三轮取各指标的中位数。基线和修改后顺序执行，避免与 GPU 测试/构建并发。

| 场景（3420×1994） | 基线完成时间 mean/P95/P99（ms） | 优化后（ms） |
| --- | --- | --- |
| Sonnet | 4.890 / 6.736 / 7.901 | 4.590 / 6.406 / 6.886 |
| Tempera | 6.321 / 12.023 / 18.324 | 6.265 / 11.354 / 15.591 |

这是短样本的提交及 GPU 等待时间，不能视作精确 GPU 时间或稳定性能提升百分比。CPU 提交耗时存在波动，本次不据此宣称 CPU 性能提升。

另外用实际 Avalonia Metal 合成器运行 Sonnet Showcase，2560×1560，预热六秒后统计八秒窗口：最终同条件对比基线 60.00 FPS，优化后 59.98 FPS；帧间隔 P99 分别 17.78 ms 和 17.73 ms；两边交换链都为三张。一次较早的优化后试跑均值为 52.37 FPS，后续复测恢复约 60 FPS，单次停顿原因未定位；因此不能把短测解释成完整播放器所有情况下都没有长尾停顿。

## 验证与边界

```sh
dotnet test src/Tests/AvaloniaSilkEffects.Tests/AvaloniaSilkEffects.Tests.csproj -c Release --no-restore
dotnet build src/Apps/KugouAvaloniaPlayer/KugouAvaloniaPlayer.csproj -c Release --no-restore
git diff --check
```

- Release 测试 191 项通过，无失败或跳过。测试工程保留两条原有 CA1416 跨平台调用警告。
- 播放器 Release 构建通过，零警告、零错误。
- 交换链测试覆盖长时间提交不完成时不扩张、不覆盖在途图像、完成后立即恢复、正常复用顺序、缩放、取消/失败和关闭回收。
- 测量探针与原始输出保留在本机 `/tmp/kugou-theme-memory-probe`；实际合成器探针在 `/tmp/kugou-theme-compositor-probe`，均为临时诊断文件。
- 尚未完成完整播放器长时间播放、音画同步、Windows 独显/核显和其他 Mac GPU 的实机验收。未修改安装版播放器，未承诺所有机器进程总内存低于 700 MB。
