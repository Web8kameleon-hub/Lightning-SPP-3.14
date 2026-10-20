import json
import os
import pathlib
import tempfile
import types
import unittest
from unittest.mock import Mock,patch
import httpx
from fastapi import HTTPException
from quality_router import QualityUnavailable
from ocean_adapter import install_ocean


class OceanTests(unittest.IsolatedAsyncioTestCase):
    def setUp(self):
        self.directory=tempfile.TemporaryDirectory(prefix='spp-ocean-fixture-')
        file=pathlib.Path(self.directory.name)/'languages.json'
        file.write_text(json.dumps({'languages':{'sq':{'name':'Albanian'}}}),encoding='utf-8')
        self.env=patch.dict(os.environ,{'OCEAN_MODEL_QUALITY_FILE':'fixture-only','OCEAN_LANGUAGES_FILE':str(file)})
        self.env.start()
        self.router=Mock()
        self.selected=types.SimpleNamespace(route_id='fixture',endpoint='https://fixture.example.test',model='fixture-model',
            digest='a'*64,capabilities=('completion','thinking'),max_context=8192)
        self.router.routes=[{'id':'fixture'}]
        self.router.select.return_value=self.selected
        self.router.discover.return_value=('a'*64,('completion','thinking'))
        async def legacy_provider(*args):return 'legacy-fixture','manual-fixture'
        async def legacy_stream(*args):yield 'legacy-fixture'
        self.namespace={'_chat_with_provider_chain':legacy_provider,'stream_ollama_response':legacy_stream,'HTTPException':HTTPException}
        with patch('ocean_adapter.configured_router',return_value=self.router):self.assertTrue(install_ocean(self.namespace))

    def tearDown(self):
        self.env.stop();self.directory.cleanup()

    async def test_provider_selects_for_explicit_language_and_keeps_manual_contract(self):
        self.router.chat.return_value=('TEST FIXTURE ONLY',self.selected)
        req=types.SimpleNamespace(model=None,language='sq')
        engines=[]
        text,model=await self.namespace['_chat_with_provider_chain'](req,'fixture','fixture safety policy','wrong-detected-tag',engines)
        self.assertEqual('TEST FIXTURE ONLY',text)
        self.assertIn('fixture-model',model)
        self.assertEqual('sq',self.router.chat.call_args.args[1])
        self.assertIn('Albanian (sq)',self.router.chat.call_args.args[0][0]['content'])
        req.model='manual'
        self.assertEqual(('legacy-fixture','manual-fixture'),await self.namespace['_chat_with_provider_chain'](req,'fixture','system','sq',[]))

    async def test_stream_checks_identity_and_keeps_actual_chunks(self):
        requests=[]
        def transport(req):
            requests.append(json.loads(req.content))
            events=[{'model':'fixture-model','message':{'content':'TEST FIXTURE '},'done':False},
                    {'model':'fixture-model','message':{'content':'ONLY'},'done':True,'done_reason':'stop'}]
            return httpx.Response(200,text='\n'.join(map(json.dumps,events)))
        real_client=httpx.AsyncClient
        with patch('httpx.AsyncClient',side_effect=lambda **kw:real_client(transport=httpx.MockTransport(transport),**kw)):
            chunks=[s async for s in self.namespace['stream_ollama_response']('old-default',[{'role':'user','content':'fixture'}],{},[],'incorrect-guess',requested_language='sq')]
        self.assertEqual(['TEST FIXTURE ','ONLY'],chunks)
        self.assertEqual('sq',self.router.select.call_args.args[0])
        self.assertIs(False,requests[0]['think'])
        self.assertEqual('fixture-model',requests[0]['model'])

    async def test_stream_digest_change_prevents_any_text_transfer(self):
        self.router.discover.return_value=('b'*64,('completion',))
        with patch('httpx.AsyncClient') as client:
            with self.assertRaises(QualityUnavailable):
                _=[s async for s in self.namespace['stream_ollama_response']('default',[{'role':'user','content':'private fixture'}],{},[],'sq',requested_language='sq')]
            client.assert_not_called()

    async def test_wrong_stream_identity_or_auth_failure_never_falls_back(self):
        real_client=httpx.AsyncClient
        for status,event in [(200,{'model':'wrong-model','message':{'content':'WRONG'},'done':True,'done_reason':'stop'}),(403,{})]:
            with patch('httpx.AsyncClient',side_effect=lambda **kw:real_client(transport=httpx.MockTransport(lambda r:httpx.Response(status,text=json.dumps(event))),**kw)):
                with self.assertRaises((QualityUnavailable,httpx.HTTPStatusError)):
                    _=[s async for s in self.namespace['stream_ollama_response']('default',[{'role':'user','content':'fixture'}],{},[],'sq',requested_language='sq')]
        self.router.chat.assert_not_called()


if __name__=='__main__':unittest.main()
