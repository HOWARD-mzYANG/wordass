# WordAss

WordAss 是一个常驻系统托盘的 Windows 划词翻译和单词本工具。运行环境为 64 位 Windows 和 .NET Framework 4.8 或更新版本。

## 使用

1. 按下方步骤构建后，双击 `build\WordAss.exe`。首次启动会打开设置，选择 API 服务并点击「验证并保存」。托盘菜单里的「设置」也可以开启「开机启动」，让应用在登录 Windows 后在托盘运行。
2. 在其他软件里用鼠标选中文字，再点击鼠标旁的「翻译」。单个英文词会显示词性、中文释义和例句，可以播放发音或加入单词本。选中句子或段落时只显示译文。
   拖动结果窗口的顶栏即可移动。点图钉可把结果固定在屏幕上，继续划词会打开新的查询窗口。再次点击图钉可取消固定，也可以用右上角的关闭按钮。
3. 如果软件没有弹出「翻译」，复制文字后从托盘菜单选择「查译剪贴板」，也可以用菜单显示的快捷键。默认是 `Ctrl+Alt+D`，占用时尝试 `Ctrl+Alt+Shift+D`。
4. 双击托盘图标打开单词本。列表显示英文与中文简释，点一行可展开详情。背完的词可以点详情右下角的垃圾桶图标并确认删除。发音使用本机安装的 Windows 英文语音。

## API 配置和本地数据

- **DeepSeek**是默认服务。填入 DeepSeek API Key 即可，默认模型为 `deepseek-flash`。旧版保存的 DeepSeek Key 会自动读取。
- **自定义 OpenAI 兼容 API**支持实现 `POST /chat/completions` 的服务。填入基础地址（例如 `https://api.openai.com/v1`）、模型名称和 API Key。无需鉴权的服务可勾选「此服务无需 API Key」。本机接口可用 HTTP，其他地址使用 HTTPS。
- 「验证并保存」会发送一条简短测试请求。日常选中的文字在点击「翻译」时发送给当前配置的服务。
- 收藏保存在 `%LOCALAPPDATA%\WordAss\wordbook.xml`，可离线查看。API 配置和 Key 通过 Windows 当前用户加密保存到同目录的 `api-settings.dat`。

## 构建

在项目目录运行以下命令，会使用 Windows 自带的 .NET Framework 编译器生成 `build\WordAss.exe`。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

## 开源协议

[MIT](LICENSE)
