"""Create reviewable overlays from exact inspected sources; never modify the input or a live service."""
import argparse
import ast
import hashlib
import json
import pathlib


def transform(source,kind):
    tree=ast.parse(source)
    lines=source.splitlines(keepends=True)
    edits=[]
    if kind=='ocean':
        required={'_chat_with_provider_chain','stream_ollama_response','chat_stream','detect_language'}
        functions={n.name:n for n in tree.body if isinstance(n,(ast.FunctionDef,ast.AsyncFunctionDef))}
        if not required<=functions.keys():raise ValueError('Ocean integration contracts changed; review required.')
        world=functions.get('languages_world')
        if world is None:raise ValueError('World-language metadata endpoint changed; review required.')
        first=world.body[1] if isinstance(world.body[0],ast.Expr) and isinstance(world.body[0].value,ast.Constant) else world.body[0]
        edits.append((first.lineno-1,0,'    if _quality_enabled():\n        return _quality_world_languages()\n'))
        for function in functions.values():
            if not any(a.arg=='req' for a in function.args.args):continue
            for node in ast.walk(function):
                if isinstance(node,ast.Call) and isinstance(node.func,ast.Name) and node.func.id=='stream_ollama_response':
                    if any(k.arg in {'requested_language','explicit_model'} for k in node.keywords):raise ValueError('Overlay already applied.')
                    # All inspected calls are multiline with the closing delimiter on its own line.
                    if lines[node.end_lineno-1].strip() not in {')','):'}:raise ValueError('Stream call layout changed; review required.')
                    previous=lines[node.end_lineno-2].rstrip()
                    if not previous.endswith(','):lines[node.end_lineno-2]=previous+',\n'
                    indent=' '*(len(lines[node.end_lineno-1])-len(lines[node.end_lineno-1].lstrip())+4)
                    edits.append((node.end_lineno-1,0,indent+'requested_language=req.language, explicit_model=req.model,\n'))
            for node in ast.walk(function):
                if function.name in {'chat_stream','chat_fast'} and isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='lang_hint' for t in node.targets):
                    indent=' '*node.col_offset
                    edits.append((node.end_lineno,0,indent+'if _quality_enabled():\n'+indent+'    lang_hint = _quality_language_instruction(req.language)\n'))
        header='from ocean_adapter import install_ocean as _install_quality, enabled as _quality_enabled, language_instruction as _quality_language_instruction\n'
    else:
        functions={n.name:n for n in tree.body if isinstance(n,(ast.FunctionDef,ast.AsyncFunctionDef))}
        required={'translate','translate_fluid_hybrid','detect_language'}
        if not required<=functions.keys():raise ValueError('Translation integration contracts changed; review required.')
        for name in ['translate','translate_fluid_hybrid']:
            node=functions[name];body=node.body[0]
            if isinstance(body,ast.Expr) and isinstance(body.value,ast.Constant):body=node.body[1]
            edits.append((body.lineno-1,0,'    if _quality_translation_active:\n        try:\n            req.target = _quality_translation_adapter.canonical(req.target)\n            if req.source != "auto": req.source = _quality_translation_adapter.canonical(req.source)\n        except ValueError as exc:\n            raise HTTPException(status_code=400, detail="Invalid language metadata") from exc\n'))
        node=functions['detect_language'];body=node.body[0]
        if isinstance(body,ast.Expr) and isinstance(body.value,ast.Constant):body=node.body[1]
        edits.append((body.lineno-1,0,'    if _quality_translation_active:\n        return await _quality_detect_request(req.text)\n'))
        header='from translation_adapter import install_translation as _install_quality\n'
    main=next((n for n in tree.body if isinstance(n,ast.If) and ast.unparse(n.test)=="__name__ == '__main__'"),None)
    insert=main.lineno-1 if main else len(lines)
    setup=header+('_quality_translation_active = _install_quality(globals())\n' if kind=='translation' else '_install_quality(globals())\n')
    edits.append((insert,0,setup+'\n'))
    for line,_,text in sorted(edits,reverse=True):lines.insert(line,text)
    output=''.join(lines)
    ast.parse(output)
    compile(output,'quality-overlay','exec')
    return output


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('kind',choices=['ocean','translation'])
    parser.add_argument('source',type=pathlib.Path)
    parser.add_argument('output',type=pathlib.Path)
    args=parser.parse_args()
    if args.output.exists():raise SystemExit('Output already exists; refusing to overwrite a reviewed candidate.')
    source=args.source.read_bytes()
    output=transform(source.decode('utf-8'),args.kind).encode('utf-8')
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_bytes(output)
    print(json.dumps({'kind':args.kind,'sourceSha256':hashlib.sha256(source).hexdigest(),
                      'outputSha256':hashlib.sha256(output).hexdigest(),'output':str(args.output)}))
