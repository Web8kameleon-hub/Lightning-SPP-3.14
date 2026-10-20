"""Optional Ocean integration. Identity is checked before text transfer and every streamed result."""
import asyncio
import json
import os
import pathlib
from quality_router import QualityUnavailable, configured_router, language_tag
from translation_adapter import TranslationAdapter

_language_metadata={}


def enabled():
    return bool(os.environ.get('OCEAN_MODEL_QUALITY_FILE','').strip())


def routing_language(language):
    # These are existing API mode values, not a list of supported languages.
    if language is None or language.strip().lower() in {'','auto','detect','default','und'}:return 'und'
    return language_tag(language.strip())


def language_instruction(language):
    if not enabled():return ''
    tag=routing_language(language)
    if tag!='und':
        name=_language_metadata.get(tag,{}).get('name',tag)
        return (' Respond in the explicitly selected language '+name+' ('+tag+'). If you cannot reliably answer in that language, disclose the limitation. ')
    return ' Respond in the language of the latest user message. Do not guess English when the language is unknown; ask for clarification if needed. '


def install_ocean(namespace):
    if not enabled():return False
    import httpx
    router=configured_router('OCEAN_MODEL_QUALITY_FILE')
    file=pathlib.Path(os.environ['OCEAN_LANGUAGES_FILE'])
    if not file.is_absolute() or file.stat().st_size>1024*1024:raise QualityUnavailable('invalid_language_catalog_file')
    adapter=TranslationAdapter(router,json.loads(file.read_text(encoding='utf-8'))['languages'])
    global _language_metadata
    _language_metadata=adapter.languages
    legacy_provider=namespace['_chat_with_provider_chain']
    legacy_stream=namespace['stream_ollama_response']

    async def provider(req,prompt,enhanced_prompt,lang_code,engines_used):
        # Explicit model overrides keep the established manual route contract.
        if req.model:return await legacy_provider(req,prompt,enhanced_prompt,lang_code,engines_used)
        language=routing_language(req.language)
        instruction=language_instruction(req.language)
        try:
            answer,selected=await asyncio.to_thread(router.chat,[{'role':'system','content':enhanced_prompt+instruction},
                {'role':'user','content':prompt}],language)
        except Exception as exc:
            raise namespace['HTTPException'](status_code=503,detail={'code':'evaluated_model_unavailable'}) from exc
        model='OllamaChat('+selected.model+')'
        engines_used.append(model)
        return answer,model

    async def stream(model,messages,options,engines_used,lang_code,allow_model_fallback=False,requested_language=None,explicit_model=None):
        if explicit_model:
            async for token in legacy_stream(model,messages,options,engines_used,lang_code,allow_model_fallback):yield token
            return
        # Called only by the explicitly configured automatic mode; never enters the legacy static fallback.
        selected=await asyncio.to_thread(router.select,routing_language(requested_language))
        route=next(r for r in router.routes if r['id']==selected.route_id)
        digest,_=await asyncio.to_thread(router.discover,route)
        if digest!=selected.digest:raise QualityUnavailable('model_changed_after_quality_selection')
        safe=[dict(m) for m in messages]
        instruction=language_instruction(requested_language)
        safe.insert(0,{'role':'system','content':instruction})
        output=options.get('num_predict',1536)
        if not isinstance(output,int) or output<1:output=1536
        if sum(len(m.get('content','').encode()) for m in safe)+output+512>selected.max_context:
            raise QualityUnavailable('context_exceeds_evaluated_route_budget')
        body={'model':selected.model,'messages':safe,'stream':True,'options':{
            **options,'temperature':0,'num_ctx':selected.max_context,'num_predict':output}}
        if 'thinking' in selected.capabilities:body['think']=False
        engines_used.append('OllamaChat('+selected.model+')')
        complete=False;total=0
        async with httpx.AsyncClient(timeout=90,follow_redirects=False,trust_env=False) as client:
            async with client.stream('POST',selected.endpoint.rstrip('/')+'/api/chat',json=body) as response:
                response.raise_for_status()
                async for line in response.aiter_lines():
                    total+=len(line.encode())
                    if total>2*1024*1024:raise QualityUnavailable('model_response_budget_exceeded')
                    if not line:continue
                    event=json.loads(line)
                    if event.get('error') or event.get('model')!=selected.model:raise QualityUnavailable('stream_identity_or_provider_error')
                    text=event.get('message',{}).get('content','')
                    if text:yield text
                    if event.get('done'):
                        if event.get('done_reason')!='stop':raise QualityUnavailable('incomplete_model_response')
                        complete=True
                        break
        if not complete:raise QualityUnavailable('incomplete_model_response')

    async def detect(text):
        result=await asyncio.to_thread(adapter.detect,text)
        return result['language'],result['name'],result['confidence']

    namespace['_chat_with_provider_chain']=provider
    namespace['stream_ollama_response']=stream
    namespace['detect_language']=detect
    def world_languages():
        return {'status':'ok','source':'operator_language_metadata','count':len(adapter.languages),
                'languages':adapter.languages,'auto_language_reply':True,
                'translator':{'routing':'digest_bound_measured_reference_fidelity','scope':'translation-reference-fidelity',
                'proficiency':'NOT_ESTABLISHED_BY_CATALOG_MEMBERSHIP'}}
    namespace['_quality_world_languages']=world_languages
    return True
