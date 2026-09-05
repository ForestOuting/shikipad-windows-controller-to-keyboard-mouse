# 第三方组件说明

ShikiPad 发布包包含以下第三方组件。该文件只记录第三方来源与许可要求，不替代相应许可证正文。

## Interception 1.0.1

- 项目：https://github.com/oblitum/Interception
- 作者：Francisco Lopes（oblitum）
- 随包文件：`interception.dll`、`driver/install-interception.exe`
- 官方许可说明：https://github.com/oblitum/Interception#license

Interception 官方说明其非商业用途采用 LGPL，并允许在程序仅通过 Interception 库及 API 与驱动通信时分发相关二进制资产；商业用途需要取得官方列出的商业许可。发布者必须根据实际用途自行确认并履行相应条款。

ShikiPad 只通过 `interception.dll` 公开 API 与 Interception 驱动通信，没有修改或嵌入 Interception 驱动代码。
