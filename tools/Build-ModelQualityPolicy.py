"""Build operator policy from measured public-reference observations, never from model names or response length."""
import argparse
import datetime as dt
import json
import pathlib
from sacrebleu.metrics import CHRF


def build(records,minimum_score,valid_days=7):
    if not 0<=minimum_score<=100 or not 0<valid_days<=31:raise ValueError('Invalid operator policy bounds.')
    latest={}
    for row in records:
        key=(row['model'],row['digest'],row['language'])
        if key not in latest or row['evaluatedAt']>latest[key]['evaluatedAt']:latest[key]=row
    measured=[r for r in latest.values() if r['state']=='MEASURED']
    if not measured:raise ValueError('No measured evidence.')
    cohorts={(r['datasetSha256'],r['metric'],r['signature'],r['scope']) for r in measured}
    if len(cohorts)!=1:raise ValueError('Incomparable benchmark cohorts.')
    for language in {r['language'] for r in measured}:
        comparable={(r['sourceLanguage'],r['referenceCode'],tuple(r['references'])) for r in measured if r['language']==language}
        if len(comparable)!=1:raise ValueError('Models were evaluated against different aligned references for '+language)
    entries=[]
    def entry(row,language=None,outputs=None,references=None):
        texts=outputs if outputs is not None else row['outputs']
        refs=references if references is not None else row['references']
        if not texts or len(texts)!=len(refs):raise ValueError('Unaligned evaluation.')
        metric=CHRF();score=metric.corpus_score(texts,[refs]).score
        signature=str(metric.get_signature())
        if signature!=row['signature']:raise ValueError('Evaluation library signature changed; reevaluate explicitly.')
        if outputs is None and abs(score-row['score'])>1e-8:raise ValueError('Stored score does not reproduce.')
        when=dt.datetime.fromisoformat(row['evaluatedAt'].replace('Z','+00:00'))
        return dict(model=row['model'],digest=row['digest'],language=language or row['language'],
            scope=row['scope'],score=score,sampleCount=len(texts),metric=row['metric'],signature=signature,
            datasetSha256=row['datasetSha256'],evaluatedAt=when.isoformat(),validUntil=(when+dt.timedelta(days=valid_days)).isoformat())
    for row in measured:entries.append(entry(row))
    models={(r['model'],r['digest']) for r in measured}
    languages=set.intersection(*[{r['language'] for r in measured if (r['model'],r['digest'])==model} for model in models])
    for model in sorted(models):
        paired=sorted([r for r in measured if (r['model'],r['digest'])==model and r['language'] in languages],key=lambda r:r['language'])
        if not paired:continue
        reference_row=min(paired,key=lambda r:r['evaluatedAt'])
        aggregate=entry(reference_row,'und',[t for r in paired for t in r['outputs']],[t for r in paired for t in r['references']])
        aggregate['languageCoverage']=sorted(languages)
        entries.append(aggregate)
    return dict(version=1,minimumScore=minimum_score,entries=entries,
        limitation='Three public reference sentences per language; chrF measures reference fidelity, not factual accuracy, native fluency or task safety.',
        aggregatePolicy='und ranks only the identical intersection of measured languages across all evaluated model digests.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('observations',type=pathlib.Path,nargs='+')
    parser.add_argument('--output',type=pathlib.Path,required=True)
    parser.add_argument('--minimum-score',type=float,required=True)
    parser.add_argument('--valid-days',type=float,default=7)
    args=parser.parse_args()
    if args.output.exists():raise SystemExit('Output already exists; use a new reviewable policy artifact.')
    rows=[json.loads(line) for path in args.observations for line in path.read_text(encoding='utf-8').splitlines()]
    policy=build(rows,args.minimum_score,args.valid_days)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(policy,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'entries':len(policy['entries']),'models':len({e['digest'] for e in policy['entries']}),'output':str(args.output)}))
