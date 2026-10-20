import json
import types
import unittest
from unittest.mock import Mock
from quality_router import QualityUnavailable
from translation_adapter import TranslationAdapter


class AdapterTests(unittest.TestCase):
    def setUp(self):
        self.router=Mock()
        self.identity=types.SimpleNamespace(route_id='fixture-route',public=lambda:{'model':'fixture-only'})
        self.adapter=TranslationAdapter(self.router,{'en':{'name':'English'},'sq':{'name':'Albanian'},'ja':{'name':'Japanese'}})

    def test_new_catalog_language_needs_no_named_branch(self):
        self.router.chat.return_value=(json.dumps({'translated':'fixture translation','source_lang':'en'}),self.identity)
        result=self.adapter.translate('fixture input','en','ja-JP')
        self.assertEqual('ja',result['target_lang'])
        self.assertEqual(0.0,result['confidence'])
        self.assertFalse(result['confidence_measured'])
        self.assertEqual('ja',self.router.chat.call_args.args[1])
        self.assertIn('Japanese (ja)',self.router.chat.call_args.args[0][0]['content'])

    def test_unknown_language_or_empty_result_is_never_success(self):
        with self.assertRaises(ValueError):self.adapter.translate('fixture','en','invalid')
        self.router.chat.return_value=(json.dumps({'translated':'','source_lang':'en'}),self.identity)
        with self.assertRaises(QualityUnavailable):self.adapter.translate('fixture','en','sq')

    def test_auto_source_remains_unknown_instead_of_english_guess(self):
        self.router.chat.return_value=(json.dumps({'translated':'fixture','source_lang':'und'}),self.identity)
        result=self.adapter.translate('fixture','auto','sq')
        self.assertEqual('und',result['source_lang'])

    def test_identical_source_target_is_identified_as_no_inference(self):
        result=self.adapter.translate('fixture','sq','sq-AL')
        self.assertEqual('fixture',result['translated'])
        self.assertEqual('IDENTICAL_SOURCE_AND_TARGET_NO_INFERENCE',result['verification'])
        self.router.chat.assert_not_called()

    def test_wrong_source_is_rejected(self):
        self.router.chat.return_value=(json.dumps({'translated':'fixture','source_lang':'ja'}),self.identity)
        with self.assertRaises(QualityUnavailable):self.adapter.translate('fixture','en','sq')


if __name__=='__main__':unittest.main()
