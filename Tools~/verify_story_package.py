"""核对 Unity 实际导出的资源与发布工程文件，不解压到工作目录。"""
import hashlib
import json
from pathlib import Path
import sys
import tarfile

archive, project = map(Path, sys.argv[1:3])
checked = 0
paths = []
retained_root_meta = []
with tarfile.open(archive, 'r:gz') as package:
    members = {m.name: m for m in package.getmembers()}
    for name, member in members.items():
        if not name.endswith('/pathname'):
            continue
        path = package.extractfile(member).read().decode('utf-8-sig').strip()
        assert path == 'Assets/SceneStory' or path.startswith('Assets/SceneStory/'), path
        paths.append(path)
        prefix = name.rsplit('/', 1)[0]
        for suffix, target in [('/asset', path), ('/asset.meta', path + '.meta')]:
            if prefix + suffix not in members:
                continue
            data = package.extractfile(members[prefix + suffix]).read()
            imported = (project / target).read_bytes()
            # Unity 保留安装前已有的父目录 GUID；只允许根目录，不放宽任何商品资源或子目录。
            if target == 'Assets/SceneStory.meta' and '--existing-root' in sys.argv and data != imported:
                retained_root_meta.append(target)
                continue
            assert data == imported, 'Content differs: ' + target
            checked += 1
    required = [
        'Editor/StageLanguage.cs', 'Editor/StageEnglishMenu.cs', 'Runtime/StageRoute.cs',
        'QuickStart.md', 'Third-Party Notices.txt',
        'Samples/StoreExamples/MultiActor.unity', 'Samples/StoreExamples/GroundLayers.unity',
        'Samples/StoreExamples/AudioAndFacing.unity', 'Samples/StoreExamples/ExampleTone.wav',
    ]
    for name in required:
        assert 'Assets/SceneStory/' + name in paths, 'Missing: ' + name
print(json.dumps({'archive': archive.name, 'bytes': archive.stat().st_size,
                  'sha256': hashlib.sha256(archive.read_bytes()).hexdigest(),
                  'checked_assets_and_meta': checked, 'retained_existing_root_meta': retained_root_meta,
                  'paths': paths}, ensure_ascii=False, indent=2))
