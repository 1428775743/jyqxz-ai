# 金庸群侠传-AI 打包说明

> 自包含单文件发布，朋友解压双击 exe 即可运行，无需装 .NET 环境。

## 前置
- .NET 8 SDK
- 7-Zip（默认路径 `C:\Program Files\7-Zip\7z.exe`）

## 打包步骤

### 1. 发布自包含单文件
```bash
dotnet publish src/AutoWuxia/AutoWuxia.csproj \
  -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o dist/publish
```

参数说明：
- `--self-contained true`：包含 .NET 运行时，朋友无需装环境
- `-p:PublishSingleFile=true`：打包成单个 exe
- `-p:IncludeNativeLibrariesForSelfExtract=true`：原生库打进单文件
- `-r win-x64`：Windows x64（其他平台改 runtime，如 linux-x64）
- `-o dist/publish`：输出目录

输出：`dist/publish/AutoWuxia.exe`（约 170M，含运行时）+ `assets/` + `data/` + `AutoWuxia.pdb`

> `assets/`、`data/` 靠 `AutoWuxia.csproj` 的 `CopyToOutputDirectory=PreserveNewest` 自动复制到输出目录，发布时自动包含。

### 2. 清理调试符号 + 重命名文件夹
```bash
rm -f dist/publish/AutoWuxia.pdb          # 删调试符号（朋友不需要）
mv dist/publish dist/金庸群侠传-AI         # 重命名为友好文件夹名
```

### 3. 打包 zip（带日期版本号）
```bash
DATE=$(date +%Y-%m-%d)
rm -f "dist/金庸群侠传-AI_$DATE.zip"       # 先删旧 zip（重要！见下方注意）
"/c/Program Files/7-Zip/7z.exe" a -tzip "dist/金庸群侠传-AI_$DATE.zip" "dist/金庸群侠传-AI"
```

> ⚠️ **必须先删旧 zip**：`7z a` 是增量添加，若 zip 已存在会保留旧数据，导致体积翻倍（实测 121M → 260M）。重新打包前务必 `rm -f` 旧 zip。

## 输出
- `dist/金庸群侠传-AI_YYYY-MM-DD.zip`（约 120M）
- zip 内含一个「金庸群侠传-AI」文件夹，解压后结构：
  ```
  金庸群侠传-AI/
  ├── AutoWuxia.exe      # 双击运行
  ├── assets/            # 图片/音乐
  └── data/              # 武功/场景/任务/角色等配置
  ```

## 朋友使用说明
1. 解压 zip
2. 双击 `AutoWuxia.exe` 启动
3. 首次进游戏点首页右上角 ⚙ 设置，填 AI API：
   - Endpoint（如 `https://api.deepseek.com` 或小米 MiMo `https://api.xiaomimimo.com`）
   - API Key
   - Model（如 `deepseek-v4-flash` / `mimo-v2.5-pro`）
4. 4K 屏可在 设置 -> 显示 调缩放倍率（125/150/200%），首页改缩放自动刷新无需重启

## 一键打包脚本
```bash
#!/bin/bash
set -e
cd /e/myxm/autowuxia
rm -rf dist/publish dist/金庸群侠传-AI
dotnet publish src/AutoWuxia/AutoWuxia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/publish
rm -f dist/publish/AutoWuxia.pdb
mv dist/publish dist/金庸群侠传-AI
DATE=$(date +%Y-%m-%d)
rm -f "dist/金庸群侠传-AI_$DATE.zip"
"/c/Program Files/7-Zip/7z.exe" a -tzip "dist/金庸群侠传-AI_$DATE.zip" "dist/金庸群侠传-AI"
echo "=== 打包完成 ==="
ls -lh "dist/金庸群侠传-AI_$DATE.zip"
```

## 注意事项
- `dist/` 不提交 git（构建产物，应加入 `.gitignore`）
- 自包含 exe 约 170M，zip 压缩后约 120M（体积主要来自 .NET 运行时 + assets 图片音乐）
- 玩家存档/配置在 `%AppData%/AutoWuxia/`（saves/ai_config.json/audio_config.json/display_config.json），与 exe 分离，解压新版不影响旧存档
- 换平台（如 linux/mac）需改 `-r` runtime 重新 publish，且 WinForms 仅 Windows
