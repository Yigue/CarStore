import requests
import time

BASE_URL = "http://localhost:8080"
LOGIN_URL = f"{BASE_URL}/api/v1/users/login"
CARS_URL = f"{BASE_URL}/api/v1/cars"
LEADS_URL = f"{BASE_URL}/api/v1/leads"
QUOTES_URL = f"{BASE_URL}/api/v1/quotes"
TIMEOUT = 30


def test_acceptquoteandtriggerdomainevents():
    session = requests.Session()
    session.timeout = TIMEOUT

    # 1. LOGIN to get token
    login_payload = {
        "email": "admin@carstore.com",
        "password": "Admin123!"
    }
    login_resp = session.post(LOGIN_URL, json=login_payload, timeout=TIMEOUT)
    assert login_resp.status_code == 200, f"Login failed: {login_resp.text}"
    token = login_resp.json().get("token")
    assert token, "No token returned in login response"
    headers = {"Authorization": f"Bearer {token}"}
    session.headers.update(headers)

    # 2. Get car id from /api/v1/cars
    cars_resp = session.get(CARS_URL, timeout=TIMEOUT)
    assert cars_resp.status_code == 200, f"Failed fetching cars: {cars_resp.text}"
    cars_data = cars_resp.json()
    assert "items" in cars_data and len(cars_data["items"]) > 0, "No cars available"
    car_id = cars_data["items"][0].get("id")
    assert car_id, "First car id not found"

    lead_id = None
    client_id = None
    quote_id = None

    try:
        # 3. Create lead
        lead_payload = {
            "clientName": "John Doe",
            "email": "johndoe@example.com",
            "phone": "1234567890",
            "source": 0,
            "notes": "Test notes",
            "interestedVehicleId": None
        }
        lead_resp = session.post(LEADS_URL, json=lead_payload, timeout=TIMEOUT)
        assert lead_resp.status_code == 201, f"Lead creation failed: {lead_resp.text}"
        lead_json = lead_resp.json()
        lead_id = lead_json.get("id")
        assert lead_id, "Lead ID not returned"

        # 4. Update lead status to 1 with notes
        status_url = f"{LEADS_URL}/{lead_id}/status"
        status_payload = {"newStatus": 1, "notes": "Called lead"}
        status_resp = session.patch(status_url, json=status_payload, timeout=TIMEOUT)
        assert status_resp.status_code == 204, f"Update lead status failed: {status_resp.text}"

        # 5. Link vehicle to lead
        vehicle_url = f"{LEADS_URL}/{lead_id}/vehicle"
        vehicle_payload = {"vehicleId": car_id}
        vehicle_resp = session.patch(vehicle_url, json=vehicle_payload, timeout=TIMEOUT)
        assert vehicle_resp.status_code == 204, f"Link vehicle failed: {vehicle_resp.text}"

        # 6. Convert lead to client
        convert_url = f"{LEADS_URL}/{lead_id}/convert"
        convert_payload = {"dni": "12345678", "address": "123 Main St"}
        convert_resp = session.post(convert_url, json=convert_payload, timeout=TIMEOUT)
        assert convert_resp.status_code == 201, f"Convert lead failed: {convert_resp.text}"
        convert_json = convert_resp.json()
        client_id = convert_json.get("clientId")
        assert client_id, "clientId not returned on conversion"

        # 7. Create quote for lead (clientId null)
        quote_payload = {
            "carId": car_id,
            "clientId": None,
            "leadId": lead_id,
            "proposedPrice": 15000.0,
            "paymentMethod": 0,
            "validUntil": "2026-07-20T00:00:00Z",
            "comments": "Quote comments"
        }
        quote_resp = session.post(QUOTES_URL, json=quote_payload, timeout=TIMEOUT)
        assert quote_resp.status_code == 201, f"Quote creation failed: {quote_resp.text}"
        quote_json = quote_resp.json()
        quote_id = quote_json.get("id")
        assert quote_id, "Quote ID not returned"

        # 8. Accept quote
        accept_url = f"{QUOTES_URL}/{quote_id}/accept"
        accept_resp = session.post(accept_url, timeout=TIMEOUT)
        assert accept_resp.status_code == 200, f"Accept quote failed: {accept_resp.text}"

        # Poll lead status up to 25s to become 'Ganado' or 4
        lead_detail_url = f"{LEADS_URL}/{lead_id}"
        status_found = False
        for _ in range(25):
            lead_detail_resp = session.get(lead_detail_url, timeout=TIMEOUT)
            if lead_detail_resp.status_code != 200:
                time.sleep(1)
                continue
            lead_detail_json = lead_detail_resp.json()
            if "status" in lead_detail_json:
                lead_status = lead_detail_json["status"]
                if lead_status == "Ganado" or lead_status == 4:
                    status_found = True
                    break
            time.sleep(1)
        assert status_found, "Lead status did not update to 'Ganado' or 4 after accepting quote"

        # 9. Verify client exists
        client_url = f"{BASE_URL}/api/v1/clients/{client_id}"
        client_resp = session.get(client_url, timeout=TIMEOUT)
        assert client_resp.status_code == 200, f"Client verification failed: {client_resp.text}"
        client_data = client_resp.json()
        assert client_data.get("id") == client_id, "Client ID mismatch on verification"
    finally:
        # Cleanup: Delete quote if deletable
        if quote_id is not None:
            try:
                session.delete(f"{QUOTES_URL}/{quote_id}", timeout=TIMEOUT)
            except Exception:
                pass
        # Cleanup: Delete client if deletable
        if client_id is not None:
            try:
                session.delete(f"{BASE_URL}/api/v1/clients/{client_id}", timeout=TIMEOUT)
            except Exception:
                pass
        # Cleanup: Delete lead
        if lead_id is not None:
            try:
                session.delete(f"{LEADS_URL}/{lead_id}", timeout=TIMEOUT)
            except Exception:
                pass


# test_acceptquoteandtriggerdomainevents()