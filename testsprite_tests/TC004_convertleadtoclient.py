import requests
import time

BASE_URL = "http://localhost:8080"


def test_convertleadtoclient():
    session = requests.Session()
    # Step 1: Login to get JWT token
    login_url = f"{BASE_URL}/api/v1/users/login"
    login_payload = {"email": "admin@carstore.com", "password": "Admin123!"}
    login_resp = session.post(login_url, json=login_payload, timeout=30)
    assert login_resp.status_code == 200, f"Login failed: {login_resp.text}"
    token = login_resp.json().get("token")
    assert token and isinstance(token, str), "Token not found in login response"
    headers = {"Authorization": f"Bearer {token}"}

    # Step 2: Fetch a valid car ID
    cars_url = f"{BASE_URL}/api/v1/cars"
    cars_resp = session.get(cars_url, headers=headers, timeout=30)
    assert cars_resp.status_code == 200, f"Get cars failed: {cars_resp.text}"
    cars_json = cars_resp.json()
    assert "items" in cars_json and isinstance(cars_json["items"], list) and len(cars_json["items"]) > 0, "Cars list empty"
    car_id = cars_json["items"][0].get("id")
    assert car_id and isinstance(car_id, str), "Car id invalid or missing"

    lead_id = None
    client_id = None
    try:
        # Step 3: Create a new lead with required fields
        leads_url = f"{BASE_URL}/api/v1/leads"
        lead_payload = {
            "clientName": "John Doe",
            "email": "johndoe@example.com",
            "phone": "1234567890",
            "source": 0,
            "notes": "Test notes",
            "interestedVehicleId": None,
        }
        lead_resp = session.post(leads_url, json=lead_payload, headers=headers, timeout=30)
        assert lead_resp.status_code == 201, f"Lead creation failed: {lead_resp.text}"
        lead_data = lead_resp.json()
        lead_id = lead_data.get("id")
        assert lead_id and isinstance(lead_id, str), "Lead id missing in creation response"

        # Step 4: Convert the lead to a client
        convert_url = f"{BASE_URL}/api/v1/leads/{lead_id}/convert"
        convert_payload = {"dni": "12345678", "address": "123 Main St"}
        convert_resp = session.post(convert_url, json=convert_payload, headers=headers, timeout=30)
        assert convert_resp.status_code == 201, f"Lead conversion failed: {convert_resp.text}"
        convert_data = convert_resp.json()
        client_id = convert_data.get("clientId")
        assert client_id and isinstance(client_id, str), "clientId missing after conversion"

        # Step 5: Verify the client record exists by fetching client
        client_url = f"{BASE_URL}/api/v1/clients/{client_id}"
        client_resp = session.get(client_url, headers=headers, timeout=30)
        assert client_resp.status_code == 200, f"Fetch client failed: {client_resp.text}"
        client_json = client_resp.json()
        # Basic verification
        assert client_json.get("id") == client_id, "Client ID mismatch"
        assert client_json.get("dni") == "12345678", "Client dni mismatch"
        assert client_json.get("address") == "123 Main St" or "address" in client_json, "Client address missing or mismatch"

    finally:
        # Cleanup - Delete the lead if created
        if lead_id:
            del_lead_url = f"{BASE_URL}/api/v1/leads/{lead_id}"
            session.delete(del_lead_url, headers=headers, timeout=30)
        # Cleanup - Delete the client if created
        if client_id:
            del_client_url = f"{BASE_URL}/api/v1/clients/{client_id}"
            session.delete(del_client_url, headers=headers, timeout=30)


test_convertleadtoclient()