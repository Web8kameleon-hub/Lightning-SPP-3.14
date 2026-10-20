"""Digest-bound, measured language-fidelity routing for configured Ollama routes.

No model-name heuristics, language keyword detection, response-length scores or
unapproved remote fallback. Configuration and evidence are operator-owned data.
"""
import datetime as dt
import json
import os
import pathlib
import re
import threading
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass


class QualityUnavailable(RuntimeError):
    pass


def language_tag(value):
    if not isinstance(value,str) or not re.fullmatch(r"[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*",value) or len(value)>85:
        raise QualityUnavailable("invalid_language_metadata")
    return value.lower()


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs):
        return None


def request_json(endpoint,path,body=None,timeout=90):
    parsed=urllib.parse.urlsplit(endpoint)
    if parsed.scheme not in {"http","https"} or not parsed.hostname or parsed.username or parsed.password or parsed.query or parsed.fragment:
        raise QualityUnavailable("invalid_operator_model_endpoint")
    req=urllib.request.Request(endpoint.rstrip('/')+'/'+path.lstrip('/'),
        data=json.dumps(body,ensure_ascii=False).encode() if body is not None else None,
        headers={"Content-Type":"application/json"})
    opener=urllib.request.build_opener(urllib.request.ProxyHandler({}),NoRedirect())
    with opener.open(req,timeout=timeout) as response:
        raw=response.read(2*1024*1024+1)
        if len(raw)>2*1024*1024:raise QualityUnavailable("model_response_budget_exceeded")
        return json.loads(raw)


def _date(value):
    parsed=dt.datetime.fromisoformat(value.replace('Z','+00:00'))
    if parsed.tzinfo is None:raise QualityUnavailable("quality_evidence_requires_timezone")
    return parsed


@dataclass(frozen=True)
class Selection:
    route_id:str
    endpoint:str
    model:str
    digest:str
    capabilities:tuple
    evidence:dict
    max_context:int

    def public(self):
        return {"route":self.route_id,"model":self.model,"digest":self.digest,"quality":self.evidence,
                "verification":"MEASURED_REFERENCE_FIDELITY_NOT_UNIVERSAL_ACCURACY"}


class QualityRouter:
    def __init__(self,config):
        if config.get('version')!=1:raise QualityUnavailable('unknown_quality_config_version')
        self.config=config
        self.routes=config.get('routes',[])
        self.entries=config.get('entries',[])
        self.minimum=config.get('minimumScore')
        if type(self.minimum) not in (int,float) or not 0<=self.minimum<=100:raise QualityUnavailable('invalid_quality_threshold')
        if not 1<=len(self.routes)<=32 or len(self.entries)>10000:raise QualityUnavailable('quality_config_budget_exceeded')
        identities=set();cohorts=set()
        for route in self.routes:
            if not route.get('id') or route['id'] in identities or not route.get('model'):raise QualityUnavailable('invalid_model_route')
            identities.add(route['id'])
            parsed=urllib.parse.urlsplit(route.get('endpoint',''))
            if parsed.scheme not in {'http','https'} or not parsed.hostname or parsed.username or parsed.password or parsed.query or parsed.fragment:
                raise QualityUnavailable('invalid_operator_model_endpoint')
            if not 2048<=route.get('maxContextTokens',8192)<=131072:raise QualityUnavailable('invalid_context_budget')
        for e in self.entries:
            language_tag(e['language'])
            if any(not re.fullmatch('[0-9a-fA-F]{64}',e[k]) for k in ['digest','datasetSha256']):raise QualityUnavailable('invalid_evidence_hash')
            if not 0<=e['score']<=100 or e['sampleCount']<1:raise QualityUnavailable('invalid_quality_measurement')
            if not dt.timedelta(0)<_date(e['validUntil'])-_date(e['evaluatedAt'])<=dt.timedelta(days=31):raise QualityUnavailable('invalid_evidence_validity')
            cohorts.add((e['datasetSha256'],e['metric'],e['signature'],e['scope']))
        if len(cohorts)>1:raise QualityUnavailable('incomparable_evaluation_cohorts')

    @classmethod
    def from_file(cls,path):
        file=pathlib.Path(path)
        if not file.is_absolute() or file.stat().st_size>4*1024*1024:raise QualityUnavailable('invalid_quality_config_file')
        return cls(json.loads(file.read_text(encoding='utf-8')))

    def evidence(self,model,digest,language,scope,now=None):
        now=now or dt.datetime.now(dt.timezone.utc)
        tag=language_tag(language)
        while tag:
            rows=[e for e in self.entries if e['model']==model and e['digest'].lower()==digest.lower() and
                  language_tag(e['language'])==tag and e['scope']==scope and _date(e['evaluatedAt'])<=now]
            if rows:
                latest=max(rows,key=lambda e:_date(e['evaluatedAt']))
                return latest if _date(latest['validUntil'])>now and latest['score']>=self.minimum else None
            tag=tag.rsplit('-',1)[0] if '-' in tag else ''
        return None

    def discover(self,route):
        tags=request_json(route['endpoint'],'api/tags',timeout=10).get('models',[])
        found=[m for m in tags if m.get('name')==route['model']]
        if len(found)!=1:raise QualityUnavailable('configured_model_identity_unavailable')
        details=request_json(route['endpoint'],'api/show',{'model':route['model']},timeout=10)
        capabilities=details.get('capabilities',[])
        if 'completion' not in capabilities:raise QualityUnavailable('model_has_no_completion_capability')
        return found[0]['digest'],tuple(capabilities)

    def select(self,language,scope='translation-reference-fidelity',allow_remote=False):
        candidates=[]
        for route in self.routes:
            if route.get('remote',False) and not allow_remote:continue
            try:digest,capabilities=self.discover(route)
            except urllib.error.HTTPError as exc:
                if exc.code in {401,403}:raise
                if exc.code==404 or exc.code==429 or exc.code>=500:continue
                raise
            except QualityUnavailable:continue
            measurement=self.evidence(route['model'],digest,language,scope)
            if measurement:candidates.append(Selection(route['id'],route['endpoint'],route['model'],digest,capabilities,measurement,route.get('maxContextTokens',8192)))
        if not candidates:raise QualityUnavailable('no_current_quality_evidence_for_language')
        if len({s.evidence['sampleCount'] for s in candidates})>1:raise QualityUnavailable('incomparable_evaluation_coverage')
        return min(candidates,key=lambda s:(-s.evidence['score'],-s.evidence['sampleCount'],s.route_id))

    def chat(self,messages,language,scope='translation-reference-fidelity',response_format=None,output_tokens=1536):
        selected=self.select(language,scope)
        route=next(r for r in self.routes if r['id']==selected.route_id)
        # Recheck the immutable model identity before sending any conversation text.
        digest,_=self.discover(route)
        if digest!=selected.digest:raise QualityUnavailable('model_changed_after_quality_selection')
        if sum(len(m.get('content','').encode()) for m in messages)+output_tokens+512>selected.max_context:
            raise QualityUnavailable('context_exceeds_evaluated_route_budget')
        body={'model':selected.model,'messages':messages,'stream':False,
              'options':{'temperature':0,'num_ctx':selected.max_context,'num_predict':output_tokens}}
        if 'thinking' in selected.capabilities:body['think']=False
        if response_format is not None:body['format']=response_format
        result=request_json(selected.endpoint,'api/chat',body)
        if result.get('model')!=selected.model or result.get('done') is not True or result.get('done_reason')!='stop':
            raise QualityUnavailable('wrong_or_incomplete_model_response')
        text=result.get('message',{}).get('content')
        if not isinstance(text,str) or not text.strip():raise QualityUnavailable('empty_model_response')
        return text,selected


_cache_lock=threading.Lock()
_cached={}


def configured_router(variable):
    path=os.environ.get(variable,'').strip()
    if not path:return None
    file=pathlib.Path(path)
    key=(str(file),file.stat().st_mtime_ns,file.stat().st_size)
    with _cache_lock:
        if key not in _cached:
            router=QualityRouter.from_file(file);_cached.clear();_cached[key]=router
        return _cached[key]
