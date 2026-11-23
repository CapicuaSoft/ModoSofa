# ModoSofá - Convierte el PC de trabajo en una consola de salón.
# Copyright (C) 2026  José Luis Bautista Martín
#
# This library is free software; you can redistribute it and/or
# modify it under the terms of the GNU Lesser General Public
# License as published by the Free Software Foundation; either
# version 2.1 of the License, or (at your option) any later version.
#
# This library is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
# Lesser General Public License for more details.
#
# You should have received a copy of the GNU Lesser General Public
# License along with this library; if not, write to the Free Software
# Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA

"""Skill de Alexa de ModoSofá: reenvía el intent al PC y dice lo que el PC responda.

Toda la lógica (qué acción ejecuta cada intent, qué decir, qué pedir confirmar) vive en el agente
(`PuenteAlexa.cs`). Esta función no hay que tocarla para añadir órdenes nuevas.

Variables de entorno:
  MODOSOFA_URL    URL pública del PC por Tailscale Funnel, sin barra final
                  (p. ej. https://mi-pc.tailnet-xxxx.ts.net:8443/alexa)
  MODOSOFA_TOKEN  Token de Alexa (icono de la bandeja → «Copiar token de Alexa»)
"""

import json
import os
import urllib.error
import urllib.request

URL = os.environ.get("MODOSOFA_URL", "").rstrip("/")
TOKEN = os.environ.get("MODOSOFA_TOKEN", "")
TIEMPO_MAXIMO = 6  # Alexa corta a los 8 s


def lambda_handler(event, context):
    peticion = event["request"]
    sesion = event.get("session") or {}
    atributos = sesion.get("attributes") or {}

    if peticion["type"] == "SessionEndedRequest":
        return {"version": "1.0", "response": {}}

    intent = "LaunchRequest" if peticion["type"] == "LaunchRequest" else peticion["intent"]["name"]

    try:
        respuesta = consultar_pc(intent, atributos.get("pendiente"))
    except urllib.error.HTTPError as e:
        texto = "El equipo ha rechazado la orden." if e.code == 401 else "El equipo ha dado un error."
        return responder(texto, True, None)
    except Exception:
        return responder("No consigo hablar con el equipo. ¿Está encendido?", True, None)

    return responder(respuesta["habla"], respuesta["terminar"], respuesta.get("pendiente"))


def consultar_pc(intent, pendiente):
    datos = json.dumps({"intent": intent, "pendiente": pendiente}).encode("utf-8")
    req = urllib.request.Request(
        URL,
        data=datos,
        method="POST",
        headers={"Content-Type": "application/json", "Authorization": f"Bearer {TOKEN}"},
    )
    with urllib.request.urlopen(req, timeout=TIEMPO_MAXIMO) as r:
        return json.loads(r.read().decode("utf-8"))


def responder(texto, terminar, pendiente):
    respuesta = {
        "outputSpeech": {"type": "PlainText", "text": texto},
        "shouldEndSession": terminar,
    }
    if not terminar:
        respuesta["reprompt"] = {"outputSpeech": {"type": "PlainText", "text": "¿Sí o no?" if pendiente else "¿Qué hago?"}}
    return {
        "version": "1.0",
        "sessionAttributes": {"pendiente": pendiente} if pendiente else {},
        "response": respuesta,
    }
