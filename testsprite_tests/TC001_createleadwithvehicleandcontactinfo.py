import requests
import time

BASE_URL = "http://localhost:8080"
TIMEOUT = 30


def test_createleadwithvehicleandcontactinfo():
    session = requests.Session()
    try:
        # Step 1: LOGIN and get token
        login_url = f"{BASE_URL}/api/v1/users/login"
        login_payload = {"email": "admin@carstore.com", "password": "Admin123!"}
        login_resp = session.post(login_url, json=login_payload, timeout=TIMEOUT)
        assert login_resp.status_code == 200, f"Login failed: {login_resp.text}"
        token = login_resp.json().get("token")
        assert token, "Token not found in login response"
        headers = {"Authorization": f"Bearer {token}"}

        # Step 2: GET /api/v1/cars - fetch first car id
        cars_url = f"{BASE_URL}/api/v1/cars"
        cars_resp = session.get(cars_url, headers=headers, timeout=TIMEOUT)
        assert cars_resp.status_code == 200, f"Failed to get cars: {cars_resp.text}"
        cars_data = cars_resp.json()
        assert "items" in cars_data and isinstance(cars_data["items"], list) and len(cars_data["items"]) > 0, "Cars list empty"
        car_id = cars_data["items"][0].get("id")
        assert car_id, "First car id not found"

        # Step 3: CREATE LEAD with given contact info, interestedVehicleId null
        create_lead_url = f"{BASE_URL}/api/v1/leads"
        lead_payload = {
            "clientName": "John Doe",
            "email": "johndoe@example.com",
            "phone": "1234567890",
            "source": 0,
            "notes": "Test notes",
            "interestedVehicleId": None
        }
        create_lead_resp = session.post(create_lead_url, headers=headers, json=lead_payload, timeout=TIMEOUT)
        assert create_lead_resp.status_code == 201, f"Create lead failed: {create_lead_resp.text}"
        lead_id = create_lead_resp.json().get("id")
        assert lead_id, "Lead ID not returned after creation"

        # Step 4: UPDATE LEAD STATUS to 1 (Contactado) with notes
        update_status_url = f"{BASE_URL}/api/v1/leads/{lead_id}/status"
        update_status_payload = {"newStatus": 1, "notes": "Called lead"}
        update_status_resp = session.patch(update_status_url, headers=headers, json=update_status_payload, timeout=TIMEOUT)
        assert update_status_resp.status_code == 204, f"Update lead status failed: {update_status_resp.text}"

        # Step 5: LINK VEHICLE to lead
        link_vehicle_url = f"{BASE_URL}/api/v1/leads/{lead_id}/vehicle"
        link_vehicle_payload = {"vehicleId": car_id}
        link_vehicle_resp = session.patch(link_vehicle_url, headers=headers, json=link_vehicle_payload, timeout=TIMEOUT)
        assert link_vehicle_resp.status_code == 204, f"Link vehicle failed: {link_vehicle_resp.text}"

        # Step 6: CONVERT LEAD
        convert_lead_url = f"{BASE_URL}/api/v1/leads/{lead_id}/convert"
        convert_payload = {"dni": "12345678", "address": "123 Main St"}
        convert_resp = session.post(convert_lead_url, headers=headers, json=convert_payload, timeout=TIMEOUT)
        assert convert_resp.status_code == 201, f"Convert lead failed: {convert_resp.text}"
        client_id = convert_resp.json().get("clientId")
        assert client_id, "Client ID not found after lead conversion"

        # Step 7: CREATE QUOTE for lead and car
        create_quote_url = f"{BASE_URL}/api/v1/quotes"
        quote_payload = {
            "carId": car_id,
            "clientId": None,
            "leadId": lead_id,
            "proposedPrice": 15000.0,
            "paymentMethod": 0,
            "validUntil": "2026-07-20T00:00:00Z",
            "comments": "Quote comments"
        }
        create_quote_resp = session.post(create_quote_url, headers=headers, json=quote_payload, timeout=TIMEOUT)
        assert create_quote_resp.status_code == 201, f"Create quote failed: {create_quote_resp.text}"
        quote_id = create_quote_resp.json().get("id")
        assert quote_id, "Quote ID not returned after creation"

        # Step 8: ACCEPT QUOTE
        accept_quote_url = f"{BASE_URL}/api/v1/quotes/{quote_id}/accept"
        accept_resp = session.post(accept_quote_url, headers=headers, timeout=TIMEOUT)
        assert accept_resp.status_code == 200, f"Accept quote failed: {accept_resp.text}"

        # Step 9: Poll lead status for up to 25s waiting for status 'Ganado' or 4
        get_lead_url = f"{BASE_URL}/api/v1/leads/{lead_id}"
        status_ok = False
        for _ in range(25):
            lead_get_resp = session.get(get_lead_url, headers=headers, timeout=TIMEOUT)
            if lead_get_resp.status_code == 200:
                lead_data = lead_get_resp.json()
                status = lead_data.get("status")
                # Status can be int 4 or string 'Ganado'
                if status == 4 or status == "Ganado":
                    status_ok = True
                    break
            time.sleep(1)
        assert status_ok, "Lead status did not update to 'Ganado' (4) within 25 seconds"

        # Step 10: CLIENT VERIFICATION by fetching client details
        get_client_url = f"{BASE_URL}/api/v1/clients/{client_id}"
        get_client_resp = session.get(get_client_url, headers=headers, timeout=TIMEOUT)
        assert get_client_resp.status_code == 200, f"Fetching client failed: {get_client_resp.text}"
        client_data = get_client_resp.json()
        # Basic checks for client data
        assert client_data.get("id") == client_id, "Client ID mismatch"
        assert client_data.get("dni") == "12345678", "Client DNI mismatch"
        assert client_data.get("address") == "123 Main St", "Client address mismatch"

    finally:
        # Cleanup - delete lead created (and cascading deletes assuming)
        if 'lead_id' in locals() and lead_id:
            delete_lead_url = f"{BASE_URL}/api/v1/leads/{lead_id}"
            session.delete(delete_lead_url, headers=headers, timeout=TIMEOUT)


# test_createleadwithvehicleandcontactinfo()