"""Tests de la Lambda sin red: se sustituye la llamada al PC.

    cd alexa/lambda && python -m unittest -v
"""

import io
import json
import unittest
import urllib.error
from unittest import mock

import lambda_function as l


def evento(tipo="IntentRequest", intent=None, atributos=None):
    peticion = {"type": tipo}
    if intent:
        peticion["intent"] = {"name": intent}
    return {"request": peticion, "session": {"attributes": atributos or {}}}


class RespuestaFalsa(io.BytesIO):
    def __enter__(self):
        return self

    def __exit__(self, *_):
        return False


def pc_responde(datos):
    return mock.patch.object(l.urllib.request, "urlopen", return_value=RespuestaFalsa(json.dumps(datos).encode()))


class LambdaTests(unittest.TestCase):
    def setUp(self):
        parche = mock.patch.object(l, "URL", "https://pc.ejemplo.ts.net:8443/alexa")
        parche.start()
        self.addCleanup(parche.stop)

    def test_sin_url_configurada_no_revienta(self):
        with mock.patch.object(l, "URL", ""):
            r = l.lambda_handler(evento(intent="CerrarJuegoIntent"), None)
        self.assertIn("encendido", r["response"]["outputSpeech"]["text"])

    def test_lanzamiento_envia_launchrequest(self):
        with pc_responde({"habla": "Listo", "terminar": False}) as urlopen:
            r = l.lambda_handler(evento("LaunchRequest"), None)
        enviado = json.loads(urlopen.call_args.args[0].data)
        self.assertEqual(enviado, {"intent": "LaunchRequest", "pendiente": None})
        self.assertFalse(r["response"]["shouldEndSession"])
        self.assertIn("reprompt", r["response"])

    def test_envia_el_token_y_la_pendiente(self):
        with mock.patch.object(l, "TOKEN", "secreto"), pc_responde({"habla": "Apagando", "terminar": True}) as urlopen:
            l.lambda_handler(evento(intent="AMAZON.YesIntent", atributos={"pendiente": "equipo.apagar"}), None)
        peticion = urlopen.call_args.args[0]
        self.assertEqual(peticion.get_header("Authorization"), "Bearer secreto")
        self.assertEqual(json.loads(peticion.data)["pendiente"], "equipo.apagar")

    def test_guarda_la_accion_pendiente_en_la_sesion(self):
        with pc_responde({"habla": "¿Seguro?", "terminar": False, "pendiente": "equipo.apagar"}):
            r = l.lambda_handler(evento(intent="ApagarEquipoIntent"), None)
        self.assertEqual(r["sessionAttributes"], {"pendiente": "equipo.apagar"})
        self.assertEqual(r["response"]["outputSpeech"]["text"], "¿Seguro?")

    def test_token_rechazado(self):
        error = urllib.error.HTTPError("u", 401, "no", {}, None)
        with mock.patch.object(l.urllib.request, "urlopen", side_effect=error):
            r = l.lambda_handler(evento(intent="CerrarJuegoIntent"), None)
        self.assertIn("rechazado", r["response"]["outputSpeech"]["text"])
        self.assertTrue(r["response"]["shouldEndSession"])

    def test_pc_apagado(self):
        with mock.patch.object(l.urllib.request, "urlopen", side_effect=TimeoutError()):
            r = l.lambda_handler(evento(intent="CerrarJuegoIntent"), None)
        self.assertIn("encendido", r["response"]["outputSpeech"]["text"])

    def test_fin_de_sesion_no_llama_al_pc(self):
        with mock.patch.object(l.urllib.request, "urlopen") as urlopen:
            r = l.lambda_handler(evento("SessionEndedRequest"), None)
        urlopen.assert_not_called()
        self.assertEqual(r["response"], {})


if __name__ == "__main__":
    unittest.main()
