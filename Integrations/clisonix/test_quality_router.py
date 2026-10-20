"""Isolated transport fixtures; these tests are not evidence of model proficiency."""
import copy
import datetime as dt
import unittest
import urllib.error
from unittest.mock import patch
from quality_router import QualityRouter, QualityUnavailable

NOW=dt.datetime.now(dt.timezone.utc)
DIGEST='a'*64


def evidence(model,score,language='sq'):
    return dict(model=model,digest=DIGEST,language=language,scope='translation-reference-fidelity',
                score=score,sampleCount=3,metric='chrF2',signature='fixture-only',datasetSha256='b'*64,
                evaluatedAt=(NOW-dt.timedelta(minutes=5)).isoformat(),validUntil=(NOW+dt.timedelta(days=1)).isoformat())


def config():
    return dict(version=1,minimumScore=40,routes=[
        dict(id='first',endpoint='https://first.example.test',model='lower'),
        dict(id='second',endpoint='https://second.example.test',model='higher')],
        entries=[evidence('lower',45),evidence('higher',80)])


class PolicyTests(unittest.TestCase):
    def test_measured_selection_ignores_route_order(self):
        router=QualityRouter(config())
        with patch.object(router,'discover',return_value=(DIGEST,('completion',))):
            self.assertEqual('higher',router.select('sq-AL').model)
            with self.assertRaises(QualityUnavailable):router.select('unmeasured')
            with self.assertRaises(QualityUnavailable):router.select('und')

    def test_latest_low_or_expired_score_does_not_resurrect_old_or_parent(self):
        c=config();new=evidence('higher',10,'sq-AL');c['entries'].append(new)
        router=QualityRouter(c)
        self.assertIsNone(router.evidence('higher',DIGEST,'sq-AL','translation-reference-fidelity'))
        new.update(score=90,validUntil=(NOW-dt.timedelta(minutes=1)).isoformat())
        router=QualityRouter(c)
        self.assertIsNone(router.evidence('higher',DIGEST,'sq-AL','translation-reference-fidelity'))
        self.assertIsNone(router.evidence('higher','c'*64,'sq','translation-reference-fidelity'))

    def test_remote_permission_checked_before_discovery(self):
        c=config();c['routes'][1]['remote']=True;router=QualityRouter(c)
        with patch.object(router,'discover',return_value=(DIGEST,('completion',))) as discover:
            self.assertEqual('lower',router.select('sq').model)
            self.assertEqual(['first'],[call.args[0]['id'] for call in discover.call_args_list])

    def test_authorization_failure_does_not_fallback(self):
        router=QualityRouter(config())
        error=urllib.error.HTTPError('https://fixture.test',403,'denied',{},None)
        with patch.object(router,'discover',side_effect=error) as discover:
            with self.assertRaises(urllib.error.HTTPError):router.select('sq')
            self.assertEqual(1,discover.call_count)

    def test_changed_digest_never_receives_conversation(self):
        router=QualityRouter(config())
        with patch.object(router,'discover',side_effect=[(DIGEST,('completion',)),(DIGEST,('completion',)),('c'*64,('completion',))]),patch('quality_router.request_json') as send:
            with self.assertRaises(QualityUnavailable):router.chat([{'role':'user','content':'private fixture'}],'sq')
            send.assert_not_called()

    def test_complete_identity_checked_and_thinking_uses_capability(self):
        router=QualityRouter(config())
        response={'model':'higher','done':True,'done_reason':'stop','message':{'content':'fixture only'}}
        with patch.object(router,'discover',return_value=(DIGEST,('completion','thinking'))),patch('quality_router.request_json',return_value=response) as send:
            answer,selected=router.chat([{'role':'user','content':'fixture'}],'sq')
            self.assertEqual('fixture only',answer)
            self.assertIs(False,send.call_args.args[2]['think'])
            for bad in [dict(response,done_reason='length'),dict(response,model='different'),dict(response,message={'content':'  '})]:
                send.return_value=bad
                with self.assertRaises(QualityUnavailable):router.chat([{'role':'user','content':'fixture'}],'sq')

    def test_mixed_cohort_and_coverage_rejected(self):
        c=config();c['entries'][1]['signature']='other'
        with self.assertRaises(QualityUnavailable):QualityRouter(c)
        c=config();c['entries'][1]['sampleCount']=6;router=QualityRouter(c)
        with patch.object(router,'discover',return_value=(DIGEST,('completion',))):
            with self.assertRaises(QualityUnavailable):router.select('sq')


if __name__=='__main__':unittest.main()
