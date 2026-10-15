# AIBuilder JSON Schemas

- `ProBuilderModelSpec.v2.schema.json`：当前场景与物体白盒的统一配置合同。
- `ProBuilderModelSpec.v2.scene.example.json`：场景白盒样例。
- `SceneSpec.v2.schema.json`、`SceneSpec.v2.example.json`：旧场景生成兼容合同，不再用于默认工作流。

Unity 执行前仍会使用 `AIBuilderSpecValidator` 做路径、数量、父子关系和参数检查；JSON Schema 负责结构与基础类型约束。
